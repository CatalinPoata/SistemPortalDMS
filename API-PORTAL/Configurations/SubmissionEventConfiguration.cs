using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class SubmissionEventConfiguration
        : BaseEntityConfiguration<SubmissionEvent>
    {
        public override void Configure(
            EntityTypeBuilder<SubmissionEvent> builder)
        {
            base.Configure(builder);

            builder.ToTable("submission_event", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_submission_event_message_length",
                    "length(message) <= 1000");

                table.HasEnumCheck<SubmissionEventType>(
                        "CK_submission_event_type",
                        "type");
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

            builder.Property(e => e.occurred_at)
                .IsRequired()
                .HasColumnType("timestamptz");

            builder.Property(e => e.type)
                .HasVarcharEnum()
                .IsRequired();

            builder.Property(e => e.message)
                .HasColumnType("text")
                .IsRequired();

            builder.HasOne(e => e.file)
                .WithMany()
                .HasForeignKey(e => e.file_id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(e => e.file_id);
        }
    }
}
