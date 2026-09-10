using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using API_DMS.Entities.Base;

namespace API_DMS.Configurations
{
    public sealed class AccountTokenConfiguration
    : BaseEntityConfiguration<AccountToken>
    {
        public override void Configure(
            EntityTypeBuilder<AccountToken> builder)
        {
            base.Configure(builder);

            builder.ToTable(
                "account_token",
                "dms",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_account_token_hash_length",
                        "length(token_hash) <= 128");

                    table.HasEnumCheck<AccountTokenPurpose>(
                        "CK_account_token_purpose",
                        "purpose");
                });

            builder.HasKey(item => item.id);

            builder.Property(item => item.id)
                .ValueGeneratedOnAdd();

            builder.Property(item => item.user_id)
                .IsRequired();

            builder.Property(item => item.token_hash)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(item => item.purpose)
                .HasVarcharEnum()
                .IsRequired();

            builder.Property(item => item.expires_at)
                .HasColumnType("timestamptz")
                .IsRequired();

            builder.Property(item => item.consumed_at)
                .HasColumnType("timestamptz");

            builder.HasOne(item => item.user)
                .WithMany()
                .HasForeignKey(item => item.user_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(item => item.token_hash)
                .IsUnique();

            builder.HasIndex(item => new
            {
                item.user_id,
                item.purpose
            });
        }
    }
}
