namespace FiapX.Identity.Domain.Users;

/// <summary>
/// A person who can log in to FIAP X. Registration and login use cases come in their own issues;
/// the scaffold only fixes the invariants the database also enforces (identity_db, ADR-0011).
/// </summary>
public sealed class User
{
    public const int MaxNameLength = 200;
    public const int MaxEmailLength = 320;

    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string Name { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private User(Guid id, string email, string name, string passwordHash, DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        Name = name;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
    }

    /// <summary>Creates a user. The password must already be hashed: the domain never sees it in clear text.</summary>
    public static User Register(Guid id, string email, string name, string passwordHash, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        var normalizedEmail = (email ?? string.Empty).Trim();
        if (normalizedEmail.Length is 0 or > MaxEmailLength || normalizedEmail.IndexOf('@', StringComparison.Ordinal) < 1)
        {
            throw new DomainException("A valid e-mail is required.");
        }

        var normalizedName = (name ?? string.Empty).Trim();
        if (normalizedName.Length is 0 or > MaxNameLength)
        {
            throw new DomainException($"Name is required and must have at most {MaxNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        return new User(id, normalizedEmail, normalizedName, passwordHash, createdAt);
    }
}
