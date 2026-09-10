using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class ReportDefinitionConfiguration
        : BaseEntityConfiguration<ReportDefinition>
    {
        public override void Configure(
            EntityTypeBuilder<ReportDefinition> builder)
        {
            base.Configure(builder);

            builder.ToTable("report_definition", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_report_definition_code_length",
                    "length(code) <= 50");

                table.HasCheckConstraint(
                    "CK_report_definition_name_length",
                    "length(name) <= 200");

                table.HasCheckConstraint(
                    "CK_report_definition_dataset_key_length",
                    "length(dataset_key) <= 50");

                table.HasCheckConstraint(
                    "CK_report_definition_version_positive",
                    "version >= 1");
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

            builder.Property(e => e.dataset_key)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.definition)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.Property(e => e.version)
                .IsRequired()
                .HasDefaultValue(1);

            builder.Property(e => e.is_system)
                .IsRequired()
                .HasDefaultValue(false);

            builder.HasOne(e => e.updated_by_user)
                .WithMany()
                .HasForeignKey(e => e.updated_by_user_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
