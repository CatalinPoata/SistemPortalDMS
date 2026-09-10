using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_DMS.Configurations
{
    public class DocumentKindConfiguration : BaseEntityConfiguration<DocumentKind>
    {
        public override void Configure(EntityTypeBuilder<DocumentKind> builder)
        {
            base.Configure(builder);

            builder.ToTable("document_kind", "dms", table =>
            {
                table.HasCheckConstraint(
                    "CK_document_kind_code_length",
                    "length(code) <= 30");

                table.HasCheckConstraint(
                    "CK_document_kind_name_length",
                    "length(name) <= 150");
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

            builder.Property(e => e.is_active)
                .IsRequired()
                .HasDefaultValue(true);
        }
    }
}
