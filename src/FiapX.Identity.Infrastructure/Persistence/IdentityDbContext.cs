using FiapX.Identity.Domain.Users;
using FiapX.Identity.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FiapX.Identity.Infrastructure.Persistence;

/// <summary>identity_db (ADR-0009). Table and column names are snake_case, matching db/schema.sql.</summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

    internal static void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}

/// <summary>Used by <c>dotnet ef</c> only; it never connects when generating migrations.</summary>
internal sealed class IdentityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Database=identity_db";
        var builder = new DbContextOptionsBuilder<IdentityDbContext>();
        IdentityDbContext.Configure(builder, connectionString);
        return new IdentityDbContext(builder.Options);
    }
}
