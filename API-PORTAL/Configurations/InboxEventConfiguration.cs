using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class InboxEventConfiguration : BaseEntityConfiguration<InboxEvent>
    {
        public override void Configure(EntityTypeBuilder<InboxEvent> builder)
        {
            base.Configure(builder);

            builder.ToTable("inbox_event", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_inbox_event_source_length",
                    "length(source) <= 40");

                table.HasCheckConstraint(
                    "CK_inbox_event_event_type_length",
                    "length(event_type) <= 80");
            });

            builder.HasKey(e => e.event_id);

            builder.Property(e => e.event_id)
                .ValueGeneratedOnAdd();

            builder.Property(e => e.source)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.event_type)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.payload)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.Property(e => e.processed_at)
                .HasColumnType("timestamptz")
                .IsRequired();
        }
    }
}
