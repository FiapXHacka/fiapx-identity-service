namespace FiapX.Identity.Application.Abstractions;

/// <summary>
/// Records an integration event to be published after the current unit of work commits
/// (transactional outbox, ADR-0007). Nothing is sent to the broker here.
/// </summary>
public interface IEventOutbox
{
    /// <param name="type">CloudEvent type, also the routing key (e.g. <c>user.registered</c>).</param>
    /// <param name="subject">Id of the aggregate the event is about.</param>
    /// <param name="dataSchemaVersion">Schema version of <paramref name="data"/> (asyncapi.yaml).</param>
    void Enqueue<TData>(string type, string subject, TData data, int dataSchemaVersion = 1)
        where TData : notnull;
}
