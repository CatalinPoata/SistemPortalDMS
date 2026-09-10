using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_DMS.Configurations
{
    public class RegistryDocumentConfiguration : BaseEntityConfiguration<RegistryDocument>
    {
        public override void Configure(EntityTypeBuilder<RegistryDocument> builder)
        {
            base.Configure(builder);

            builder.ToTable("registry_document", "dms", table =>
            {
                table.HasCheckConstraint(
                    "CK_registry_document_issuer_length",
                    "issuer IS NULL OR length(issuer) <= 200");

                table.HasCheckConstraint(
                    "CK_registry_document_note_length",
                    "note IS NULL OR length(note) <= 500");

                table.HasCheckConstraint(
                    "CK_registry_document_storage_key_length",
                    "length(storage_key) <= 200");

                table.HasCheckConstraint(
                    "CK_registry_document_original_name_length",
                    "length(original_name) <= 255");

                table.HasCheckConstraint(
                    "CK_registry_document_content_type_length",
                    "length(content_type) <= 120");

                table.HasCheckConstraint(
                    "CK_registry_document_sha256_length",
                    "length(sha256) <= 64");

                table.HasEnumCheck<DocumentDirection>(
                    "CK_registry_document_direction",
                    "direction");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.HasOne(e => e.entry)
                .WithMany()
                .HasForeignKey(e => e.entry_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => e.external_file_id)
                .IsUnique();

            builder.Property(e => e.direction)
                .IsRequired();

            builder.Property(e => e.direction)
                .HasVarcharEnum()
                .IsRequired();

            builder.HasOne(e => e.document_kind)
                .WithMany()
                .HasForeignKey(e => e.document_kind_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(e => e.document_date)
                .HasColumnType("date");

            builder.Property(e => e.issuer)
                .HasColumnType("text");

            builder.Property(e => e.note)
                .HasColumnType("text");

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
                .IsRequired();

            builder.Property(e => e.sha256)
                .HasColumnType("text")
                .IsRequired();

            builder.HasOne(e => e.uploaded_by_user)
                .WithMany()
                .HasForeignKey(e => e.uploaded_by_user_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
