using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class UserConfiguration : BaseEntityConfiguration<User>
    {
        public override void Configure(EntityTypeBuilder<User> builder)
        {
            base.Configure(builder);

            builder.ToTable("user", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_user_email_length",
                    "length(email) <= 256");

                table.HasCheckConstraint(
                    "CK_user_password_hash_length",
                    "length(password_hash) <= 512");

                table.HasCheckConstraint(
                    "CK_user_full_name_length",
                    "length(full_name) <= 200");

                table.HasCheckConstraint(
                    "CK_user_national_id_length",
                    "national_id IS NULL OR length(national_id) <= 13");

                table.HasCheckConstraint(
                    "CK_user_phone_length",
                    "phone IS NULL OR length(phone) <= 30");

                table.HasCheckConstraint(
                    "CK_user_address_length",
                    "address IS NULL OR length(address) <= 500");

                table.HasCheckConstraint(
                    "CK_user_role",
                    "role IN ('Citizen', 'Admin')");
            });

            builder.HasKey(e => e.id);
            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.Property(e => e.email)
                .IsRequired()
                .HasColumnType("text");
            builder.HasIndex(e => e.email)
                .IsUnique();

            builder.Property(e => e.password_hash)
                .IsRequired()
                .HasColumnType("text");

            builder.Property(e => e.role)
                .HasVarcharEnum()
                .IsRequired()
                .HasDefaultValueSql("'Citizen'");

            builder.Property(e => e.full_name)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.national_id)
                .HasColumnType("text");

            builder.Property(e => e.phone)
                .HasColumnType("text");

            builder.Property(e => e.address)
                .HasColumnType("text");

            builder.Property(e => e.email_confirmed)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(e => e.is_active)
                .IsRequired()
                .HasDefaultValue(true);


            builder.Property(e => e.lockout_end)
                .HasColumnType("timestamptz");
        }
    }
}
