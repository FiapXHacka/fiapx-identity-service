namespace FiapX.Identity.Application.IntegrationEvents;

/// <summary>Data of the <c>user.registered</c> event (fiapx/docs/contracts/asyncapi.yaml).</summary>
public sealed record UserRegistered(Guid UserId, string Email, string Name)
{
    public const string EventType = "user.registered";
}
