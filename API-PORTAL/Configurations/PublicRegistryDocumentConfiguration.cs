using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class PublicRegistryDocumentConfiguration
        : BaseEntityConfiguration<PublicRegistryDocument>
    {
        public override void Configure(
            EntityTypeBuilder<PublicRegistryDocument> builder)
        {
            base.Configure(builder);

            builder.ToTable("public_registry_document", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_public_registry_document_storage_key_length",
                    "length(storage_key) <= 200");

                table.HasCheckConstraint(
                    "CK_public_registry_document_original_name_length",
                    "length(original_name) <= 255");

                table.HasCheckConstraint(
                    "CK_public_registry_document_content_type_length",
                    "length(content_type) <= 120");

                table.HasCheckConstraint(
                    "CK_public_registry_document_sha256_length",
                    "length(sha256) <= 64");

                table.HasCheckConstraint(
                    "CK_public_registry_document_size_bytes_non_negative",
                    "size_bytes >= 0");

                table.HasCheckConstraint(
                    "CK_public_registry_document_display_order_non_negative",
                    "display_order >= 0");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.HasOne(e => e.entry)
                .WithMany()
                .HasForeignKey(e => e.entry_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => e.entry_id);

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
                .HasColumnType("bigint")
                .IsRequired();

            builder.Property(e => e.sha256)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.display_order)
                .IsRequired()
                .HasDefaultValue(0);

            builder.HasIndex(e => new
            {
                e.entry_id,
                e.display_order
            });
        }
    }
}
