using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class PublicRegistryEntryConfiguration
        : BaseEntityConfiguration<PublicRegistryEntry>
    {
        public override void Configure(
            EntityTypeBuilder<PublicRegistryEntry> builder)
        {
            base.Configure(builder);

            builder.ToTable("public_registry_entry", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_public_registry_entry_position_number_length",
                    "length(position_number) <= 40");

                table.HasCheckConstraint(
                    "CK_public_registry_entry_title_length",
                    "length(title) <= 500");

                table.HasCheckConstraint(
                    "CK_public_registry_entry_description_length",
                    "description IS NULL OR length(description) <= 2000");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.HasOne(e => e.registry)
                .WithMany()
                .HasForeignKey(e => e.registry_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new
            {
                e.registry_id,
                e.position_number
            })
            .IsUnique();

            builder.Property(e => e.position_number)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.title)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.entry_date)
                .HasColumnType("date")
                .IsRequired();

            builder.Property(e => e.description)
                .HasColumnType("text");

            builder.Property(e => e.is_published)
                .IsRequired()
                .HasDefaultValue(false);

            builder.HasIndex(e => new
            {
                e.registry_id,
                e.is_published
            });
        }
    }
}
