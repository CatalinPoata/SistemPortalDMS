using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class PublicRegistryConfiguration : BaseEntityConfiguration<PublicRegistry>
    {
        public override void Configure(EntityTypeBuilder<PublicRegistry> builder)
        {
            base.Configure(builder);

            builder.ToTable("public_registry", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_public_registry_code_length",
                    "length(code) <= 40");

                table.HasCheckConstraint(
                    "CK_public_registry_name_length",
                    "length(name) <= 200");

                table.HasCheckConstraint(
                    "CK_public_registry_description_length",
                    "description IS NULL OR length(description) <= 1000");

                table.HasCheckConstraint(
                    "CK_public_registry_display_order_non_negative",
                    "display_order >= 0");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.Property(e => e.code)
                .HasColumnType("text")
                .IsRequired();

            builder.HasIndex(e => e.code)
                .IsUnique();

            builder.Property(e => e.name)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.description)
                .HasColumnType("text");

            builder.Property(e => e.is_published)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(e => e.display_order)
                .IsRequired()
                .HasDefaultValue(0);

        }
    }
}
