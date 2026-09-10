using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_DMS.Configurations
{
    public class RegistryEntryConfiguration : BaseEntityConfiguration<RegistryEntry>
    {
        public override void Configure(EntityTypeBuilder<RegistryEntry> builder)
        {
            base.Configure(builder);

            builder.ToTable("registry_entry", "dms", table =>
            {
                table.HasCheckConstraint(
                    "CK_registry_entry_subject_length",
                    "length(subject) <= 1000");

                table.HasCheckConstraint(
                    "CK_registry_entry_applicant_name_length",
                    "length(applicant_name) <= 200");

                table.HasCheckConstraint(
                    "CK_registry_entry_applicant_national_id_length",
                    "applicant_national_id IS NULL OR length(applicant_national_id) <= 13");

                table.HasCheckConstraint(
                    "CK_registry_entry_applicant_email_length",
                    "applicant_email IS NULL OR length(applicant_email) <= 256");

                table.HasCheckConstraint(
                    "CK_registry_entry_applicant_phone_length",
                    "applicant_phone IS NULL OR length(applicant_phone) <= 30");

                table.HasCheckConstraint(
                    "CK_registry_entry_applicant_address_length",
                    "applicant_address IS NULL OR length(applicant_address) <= 500");

                table.HasCheckConstraint(
                    "CK_registry_entry_source_doc_number_length",
                    "source_doc_number IS NULL OR length(source_doc_number) <= 60");

                table.HasCheckConstraint(
                    "CK_registry_entry_service_code_length",
                    "service_code IS NULL OR length(service_code) <= 50");

                table.HasCheckConstraint(
                    "CK_registry_entry_status_note_length",
                    "status_note IS NULL OR length(status_note) <= 1000");

                table.HasEnumCheck<EntryDirection>(
                    "CK_registry_entry_direction",
                    "direction");

                table.HasEnumCheck<EntryStatus>(
                    "CK_registry_entry_status",
                    "status");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.HasIndex(e => e.external_id)
                .IsUnique();

            builder.HasOne(e => e.registry_type)
                .WithMany()
                .HasForeignKey(e => e.registry_type_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => new
            {
                e.registry_type_id,
                e.year,
                e.number
            })
            .IsUnique();

            builder.HasIndex(e => new
            {
                e.registry_type_id,
                e.year,
                e.registered_at
            });

            builder.HasIndex(e => e.status);

            builder.HasIndex(e => e.department_id);

            builder.HasIndex(e => e.subject)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            builder.HasIndex(e => e.applicant_name)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            builder.Property(e => e.year)
                .IsRequired();

            builder.Property(e => e.number)
                .IsRequired();

            builder.Property(e => e.direction)
                .HasVarcharEnum()
                .IsRequired()
                .HasDefaultValueSql("'In'");

            builder.Property(e => e.registered_at)
                .IsRequired()
                .HasColumnType("timestamptz");

            builder.Property(e => e.submitted_at)
                .HasColumnType("timestamptz");

            builder.Property(e => e.subject)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.applicant_name)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.applicant_national_id)
                .HasColumnType("text");

            builder.Property(e => e.applicant_email)
                .HasColumnType("text");

            builder.Property(e => e.applicant_phone)
                .HasColumnType("text");

            builder.Property(e => e.applicant_address)
                .HasColumnType("text");

            builder.Property(e => e.source_doc_number)
                .HasColumnType("text");

            builder.Property(e => e.source_doc_date)
                .HasColumnType("date");

            builder.HasOne(e => e.department)
                .WithMany()
                .HasForeignKey(e => e.department_id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Property(e => e.service_code)
                .HasColumnType("text");

            builder.Property(e => e.form_values)
                .HasColumnType("jsonb");

            builder.Property(e => e.deadline)
                .IsRequired()
                .HasColumnType("date");

            builder.Property(e => e.status)
                .HasVarcharEnum()
                .IsRequired()
                .HasDefaultValueSql("'Registered'");

            builder.Property(e => e.status_note)
                .HasColumnType("text");

            builder.HasOne(e => e.created_by_user)
                .WithMany()
                .HasForeignKey(e => e.created_by_user_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
