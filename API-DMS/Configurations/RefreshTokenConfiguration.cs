using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_DMS.Configurations
{
    public class RefreshTokenConfiguration : BaseEntityConfiguration<RefreshToken>
    {
        public override void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            base.Configure(builder);

            builder.ToTable("refresh_token", "dms", table =>
            {
                table.HasCheckConstraint(
                    "CK_refresh_token_token_hash_length",
                    "length(token_hash) <= 128");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.Property(e => e.token_hash)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.expires_at)
                .HasColumnType("timestamptz")
                .IsRequired();

            builder.Property(e => e.revoked_at)
                .HasColumnType("timestamptz");

            builder.HasOne(e => e.user)
                .WithMany()
                .HasForeignKey(e => e.user_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.replaced_by)
                .WithMany()
                .HasForeignKey(e => e.replaced_by_id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => e.token_hash)
                .IsUnique();
        }
    }
}
