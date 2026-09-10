using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_DMS.Configurations
{
    public class EntryEventConfiguration : BaseEntityConfiguration<EntryEvent>
    {
        public override void Configure(EntityTypeBuilder<EntryEvent> builder)
        {
            base.Configure(builder);

            builder.ToTable("entry_event", "dms", table =>
            {
                table.HasCheckConstraint(
                    "CK_entry_event_message_length",
                    "length(message) <= 1000");

                table.HasEnumCheck<EventType>(
                    "CK_entry_event_type",
                    "type");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.HasOne(e => e.entry)
                .WithMany()
                .HasForeignKey(e => e.entry_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(e => e.occurred_at)
                .IsRequired()
                .HasColumnType("timestamptz");

            builder.HasOne(e => e.actor_user)
                .WithMany()
                .HasForeignKey(e => e.actor_user_id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Property(e => e.type)
                .HasVarcharEnum()
                .IsRequired();

            builder.Property(e => e.message)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.payload)
                .HasColumnType("jsonb");
        }
    }
}
