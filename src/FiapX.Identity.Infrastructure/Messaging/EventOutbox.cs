using FiapX.Identity.Application.Abstractions;
using FiapX.Identity.Infrastructure.Persistence;

namespace FiapX.Identity.Infrastructure.Messaging;

/// <summary>
/// Adds the event to the DbContext; it is committed by the caller's SaveChanges together with the
/// business change, and published later by <see cref="OutboxRelay"/>.
/// </summary>
internal sealed class EventOutbox(IdentityDbContext db, CloudEventFactory factory) : IEventOutbox
{
    public void Enqueue<TData>(string type, string subject, TData data, int dataSchemaVersion = 1)
        where TData : notnull
    {
        var cloudEvent = factory.Create(type, subject, data, dataSchemaVersion);
        db.OutboxMessages.Add(OutboxMessage.From(cloudEvent));
    }
}
