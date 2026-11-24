using FiapX.Identity.Domain;
using FiapX.Identity.Domain.Users;

namespace FiapX.Identity.UnitTests.Domain;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 18, 0, 0, TimeSpan.Zero);
    private static readonly Guid Id = Guid.CreateVersion7(Now);

    [Fact]
    public void Register_trims_and_keeps_the_values()
    {
        var user = User.Register(Id, "  Ana@Example.com ", "  Ana Souza ", "hash", Now);

        user.Id.ShouldBe(Id);
        user.Email.ShouldBe("Ana@Example.com");
        user.Name.ShouldBe("Ana Souza");
        user.PasswordHash.ShouldBe("hash");
        user.CreatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("@starts-with-at.com")]
    public void Register_rejects_invalid_emails(string email) =>
        Should.Throw<DomainException>(() => User.Register(Id, email, "Ana", "hash", Now))
            .Message.ShouldContain("e-mail");

    [Fact]
    public void Register_rejects_too_long_email()
    {
        var email = new string('a', User.MaxEmailLength) + "@x.com";

        Should.Throw<DomainException>(() => User.Register(Id, email, "Ana", "hash", Now));
    }

    [Fact]
    public void Register_rejects_null_email_and_name()
    {
        Should.Throw<DomainException>(() => User.Register(Id, null!, "Ana", "hash", Now));
        Should.Throw<DomainException>(() => User.Register(Id, "ana@example.com", null!, "hash", Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Register_rejects_empty_name(string name) =>
        Should.Throw<DomainException>(() => User.Register(Id, "ana@example.com", name, "hash", Now))
            .Message.ShouldContain("Name");

    [Fact]
    public void Register_rejects_too_long_name() =>
        Should.Throw<DomainException>(() =>
            User.Register(Id, "ana@example.com", new string('a', User.MaxNameLength + 1), "hash", Now));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Register_requires_a_password_hash(string hash) =>
        Should.Throw<DomainException>(() => User.Register(Id, "ana@example.com", "Ana", hash, Now))
            .Message.ShouldContain("Password");

    [Fact]
    public void Register_requires_an_id() =>
        Should.Throw<DomainException>(() => User.Register(Guid.Empty, "ana@example.com", "Ana", "hash", Now));

    [Fact]
    public void DomainException_constructors_keep_message_and_inner_exception()
    {
        var inner = new InvalidOperationException("inner");

        new DomainException("msg", inner).InnerException.ShouldBe(inner);
        new DomainException().Message.ShouldNotBeNullOrEmpty();
    }
}
