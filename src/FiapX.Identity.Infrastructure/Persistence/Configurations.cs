using FiapX.Identity.Domain.Users;
using FiapX.Identity.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FiapX.Identity.Infrastructure.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", t => t.HasCheckConstraint("users_email_format", "position('@' in email) > 1"));
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.Name).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
        // The case-insensitive unique index on lower(email) is an expression index: created in the migration (raw SQL).
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).IsRequired();
        builder.Property(m => m.Payload).IsRequired().HasColumnType("jsonb");
        builder.Property(m => m.OccurredAt).HasDefaultValueSql("now()");
        builder.Property(m => m.Attempts).HasDefaultValue(0);
        builder.HasIndex(m => m.OccurredAt)
            .HasDatabaseName("ix_outbox_pending")
            .HasFilter("published_at IS NULL");
    }
}
