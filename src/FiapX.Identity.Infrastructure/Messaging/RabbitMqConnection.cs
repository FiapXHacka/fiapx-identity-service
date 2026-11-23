using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FiapX.Identity.Infrastructure.Messaging;

/// <summary>
/// One long-lived AMQP connection per process, opened lazily so the API starts (and keeps accepting
/// requests, writing to the outbox) even when the broker is down.
/// </summary>
public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public bool IsOpen => _connection?.IsOpen == true;

    public async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true } open)
        {
            return open;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is { IsOpen: true } opened)
            {
                return opened;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
            }

            var settings = options.Value;
            var factory = new ConnectionFactory
            {
                Uri = settings.Uri ?? throw new InvalidOperationException("RabbitMq:Uri is not configured."),
                ClientProvidedName = settings.ClientName,
                AutomaticRecoveryEnabled = true,
            };
            _connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        _gate.Dispose();
    }
}
