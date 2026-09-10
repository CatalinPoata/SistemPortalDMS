using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class SubmissionFileConfiguration
        : BaseEntityConfiguration<SubmissionFile>
    {
        public override void Configure(
            EntityTypeBuilder<SubmissionFile> builder)
        {
            base.Configure(builder);

            builder.ToTable("submission_file", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_submission_file_field_key_length",
                    "field_key IS NULL OR length(field_key) <= 60");

                table.HasCheckConstraint(
                    "CK_submission_file_storage_key_length",
                    "length(storage_key) <= 200");

                table.HasCheckConstraint(
                    "CK_submission_file_original_name_length",
                    "length(original_name) <= 255");

                table.HasCheckConstraint(
                    "CK_submission_file_content_type_length",
                    "length(content_type) <= 120");

                table.HasCheckConstraint(
                    "CK_submission_file_sha256_length",
                    "length(sha256) <= 64");

                table.HasEnumCheck<SubmissionFileKind>(
                    "CK_submission_file_kind",
                    "kind");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.HasOne(e => e.submission)
                .WithMany()
                .HasForeignKey(e => e.submission_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => e.submission_id);

            builder.Property(e => e.field_key)
                .HasColumnType("text");

            builder.Property(e => e.kind)
                .HasVarcharEnum()
                .IsRequired();

            builder.Property(e => e.storage_key)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.original_name)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.content_type)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.size_bytes)
                .IsRequired()
                .HasColumnType("bigint");

            builder.Property(e => e.sha256)
                .HasColumnType("text")
                .IsRequired();
        }
    }
}
