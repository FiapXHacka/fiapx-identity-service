using System.ComponentModel.DataAnnotations;

namespace FiapX.Identity.Infrastructure.Security;

/// <summary>
/// Token settings shared by the platform (section <c>Auth</c>, ADR-0011). The identity-service signs tokens
/// with this issuer and audience (issue #3); the other services validate them with the same values.
/// </summary>
public sealed class AuthOptions : IValidatableObject
{
    public const string SectionName = "Auth";
    public const string DefaultAudience = "fiapx-api";

    /// <summary>Public issuer URL, e.g. the gateway (<c>http://localhost:8080</c> locally). Required once tokens are issued (#3).</summary>
    public Uri? Issuer { get; set; }

    [Required]
    public string Audience { get; set; } = DefaultAudience;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Issuer is { IsAbsoluteUri: false })
        {
            yield return new ValidationResult("Auth:Issuer must be an absolute URL.", [nameof(Issuer)]);
        }
    }
}
