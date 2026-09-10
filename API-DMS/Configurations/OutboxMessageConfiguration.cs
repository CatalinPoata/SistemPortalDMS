using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_DMS.Configurations
{
    public class OutboxMessageConfiguration
        : BaseEntityConfiguration<OutboxMessage>
    {
        public override void Configure(
            EntityTypeBuilder<OutboxMessage> builder)
        {
            base.Configure(builder);

            builder.ToTable("outbox_message", "dms", table =>
            {
                table.HasCheckConstraint(
                    "CK_outbox_message_aggregate_type_length",
                    "length(aggregate_type) <= 60");

                table.HasCheckConstraint(
                    "CK_outbox_message_event_type_length",
                    "length(event_type) <= 80");

                table.HasCheckConstraint(
                    "CK_outbox_message_last_error_length",
                    "last_error IS NULL OR length(last_error) <= 2000");

                table.HasCheckConstraint(
                    "CK_outbox_message_attempts_nonnegative",
                    "attempts >= 0");

                table.HasEnumCheck<OutboxMessageStatus>(
                    "CK_outbox_message_status",
                    "status");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.Property(e => e.aggregate_type)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.aggregate_id)
                .IsRequired();

            builder.Property(e => e.event_type)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.payload)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.Property(e => e.status)
                .HasVarcharEnum()
                .IsRequired()
                .HasDefaultValueSql("'Pending'");

            builder.Property(e => e.attempts)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(e => e.next_attempt_at)
                .HasColumnType("timestamptz")
                .IsRequired();

            builder.Property(e => e.last_error)
                .HasColumnType("text");

            builder.Property(e => e.delivered_at)
                .HasColumnType("timestamptz");

            builder.HasIndex(e => new
            {
                e.status,
                e.next_attempt_at
            });
        }
    }
}
