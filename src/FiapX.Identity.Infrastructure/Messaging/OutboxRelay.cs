using System.Text;
using FiapX.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FiapX.Identity.Infrastructure.Messaging;

/// <summary>Outcome of one relay pass.</summary>
public readonly record struct OutboxBatchResult(int Published, bool Failed);

/// <summary>
/// Publishes pending outbox messages in order (ADR-0007). Rows are locked with
/// <c>FOR UPDATE SKIP LOCKED</c>, so several replicas can run the relay without publishing twice.
/// A failure stops the batch (keeps the order) and the loop backs off exponentially.
/// </summary>
public sealed partial class OutboxRelay(
    IServiceScopeFactory scopeFactory,
    IMessagePublisher publisher,
    TimeProvider timeProvider,
    IOptions<OutboxRelayOptions> options,
    ILogger<OutboxRelay> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var backoff = settings.PollInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                var result = await PublishPendingAsync(stoppingToken).ConfigureAwait(false);
                if (result.Failed)
                {
                    backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, settings.MaxBackoff.Ticks));
                    delay = backoff;
                }
                else
                {
                    backoff = settings.PollInterval;
                    // A full batch means there is probably more waiting: go again right away.
                    delay = result.Published == settings.BatchSize ? TimeSpan.Zero : settings.PollInterval;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // The relay must survive any failure (database down, etc.) and retry.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogRelayFailed(ex);
                backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, settings.MaxBackoff.Ticks));
                delay = backoff;
            }

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, timeProvider, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>Publishes one batch of pending messages. Public so tests can drive the relay deterministically.</summary>
    public async Task<OutboxBatchResult> PublishPendingAsync(CancellationToken cancellationToken)
    {
        var batchSize = options.Value.BatchSize;
        var scope = scopeFactory.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                var pending = await db.OutboxMessages
                    .FromSql($"SELECT * FROM outbox_messages WHERE published_at IS NULL ORDER BY occurred_at LIMIT {batchSize} FOR UPDATE SKIP LOCKED")
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                var published = 0;
                var failed = false;
                foreach (var message in pending)
                {
                    try
                    {
                        await publisher.PublishAsync(
                            new OutgoingMessage(message.Id.ToString(), message.Type, Encoding.UTF8.GetBytes(message.Payload), message.OccurredAt),
                            cancellationToken).ConfigureAwait(false);
                        message.MarkPublished(timeProvider.GetUtcNow());
                        published++;
                    }
#pragma warning disable CA1031 // Any broker failure is recorded on the message and retried later.
                    catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
                    {
                        message.RegisterFailedAttempt();
                        LogPublishFailed(ex, message.Id, message.Type, message.Attempts);
                        failed = true;
                        break;
                    }
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                if (published > 0)
                {
                    LogPublished(published);
                }

                return new OutboxBatchResult(published, failed);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox relay published {Count} message(s)")]
    private partial void LogPublished(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not publish outbox message {MessageId} ({Type}), attempt {Attempts}; will retry")]
    private partial void LogPublishFailed(Exception exception, Guid messageId, string type, int attempts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox relay pass failed; backing off")]
    private partial void LogRelayFailed(Exception exception);
}
