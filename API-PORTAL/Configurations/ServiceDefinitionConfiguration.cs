using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class ServiceDefinitionConfiguration
        : BaseEntityConfiguration<ServiceDefinition>
    {
        public override void Configure(
            EntityTypeBuilder<ServiceDefinition> builder)
        {
            base.Configure(builder);

            builder.ToTable("service_definition", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_service_definition_code_length",
                    "length(code) <= 50");

                table.HasCheckConstraint(
                    "CK_service_definition_code_format",
                    "code ~ '^[a-z0-9-]+$'");

                table.HasCheckConstraint(
                    "CK_service_definition_title_length",
                    "length(title) <= 200");

                table.HasCheckConstraint(
                    "CK_service_definition_short_description_length",
                    "short_description IS NULL OR length(short_description) <= 500");

                table.HasCheckConstraint(
                    "CK_service_definition_registry_type_code_length",
                    "length(registry_type_code) <= 30");

                table.HasCheckConstraint(
                    "CK_service_definition_schema_version_positive",
                    "schema_version >= 1");

                table.HasCheckConstraint(
                    "CK_service_definition_max_attachments_non_negative",
                    "max_attachments >= 0");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.Property(e => e.code)
                .HasColumnType("text")
                .IsRequired();

            builder.HasIndex(e => e.code)
                .IsUnique();

            builder.Property(e => e.title)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.short_description)
                .HasColumnType("text");

            builder.Property(e => e.description)
                .HasColumnType("text");

            builder.Property(e => e.registry_type_code)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.form_schema)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.Property(e => e.schema_version)
                .IsRequired()
                .HasDefaultValue(1);

            builder.Property(e => e.requires_attachment)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(e => e.max_attachments)
                .IsRequired()
                .HasDefaultValue(3);

            builder.Property(e => e.is_published)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(e => e.display_order)
                .IsRequired()
                .HasDefaultValue(0);
        }
    }
}
