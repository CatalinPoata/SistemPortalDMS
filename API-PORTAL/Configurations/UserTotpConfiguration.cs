using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations;

public sealed class UserTotpConfiguration
    : BaseEntityConfiguration<UserTotp>
{
    public override void Configure(EntityTypeBuilder<UserTotp> builder)
    {
        base.Configure(builder);

        builder.ToTable("user_totp", "portal", table =>
        {
            table.HasCheckConstraint(
                "CK_user_totp_secret_length",
                "length(protected_secret) <= 2048");
        });

        builder.HasKey(item => item.id);
        builder.Property(item => item.id).ValueGeneratedOnAdd();

        builder.Property(item => item.user_id).IsRequired();
        builder.HasIndex(item => item.user_id).IsUnique();

        builder.HasOne(item => item.user)
            .WithMany()
            .HasForeignKey(item => item.user_id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(item => item.protected_secret)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(item => item.is_enabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(item => item.last_used_counter)
            .HasColumnType("bigint");

        builder.Property(item => item.recovery_code_hashes)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(item => item.enabled_at)
            .HasColumnType("timestamptz");
    }
}
