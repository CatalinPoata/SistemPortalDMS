using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class SubmissionConfiguration
       : BaseEntityConfiguration<Submission>
    {
        public override void Configure(
            EntityTypeBuilder<Submission> builder)
        {
            base.Configure(builder);

            builder.ToTable("submission", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_submission_status_details_length",
                    "status_details IS NULL OR length(status_details) <= 1000");

                table.HasCheckConstraint(
                    "CK_submission_registry_display_number_length",
                    "registry_display_number IS NULL OR length(registry_display_number) <= 30");

                table.HasEnumCheck<SubmissionStatus>(
                    "CK_submission_status",
                    "status");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.Property(e => e.external_id)
                .IsRequired();

            builder.HasIndex(e => e.external_id)
                .IsUnique();

            builder.HasOne(e => e.service)
                .WithMany()
                .HasForeignKey(e => e.service_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(e => e.schema_version)
                .IsRequired();

            builder.Property(e => e.form_snapshot)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.HasOne(e => e.user)
                .WithMany()
                .HasForeignKey(e => e.user_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(e => e.values)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.Property(e => e.status)
                .HasVarcharEnum()
                .IsRequired()
                .HasDefaultValueSql("'Submitted'");

            builder.Property(e => e.status_details)
                .HasColumnType("text");

            builder.Property(e => e.submitted_at)
                .HasColumnType("timestamptz")
                .IsRequired();

            builder.Property(e => e.registry_number)
                .HasColumnType("bigint");

            builder.Property(e => e.registry_year)
                .HasColumnType("integer");

            builder.Property(e => e.registry_display_number)
                .HasColumnType("text");

            builder.Property(e => e.registered_at)
                .HasColumnType("timestamptz");

            builder.Property(e => e.dms_entry_id)
                .HasColumnType("uuid");
        }
    }
}
