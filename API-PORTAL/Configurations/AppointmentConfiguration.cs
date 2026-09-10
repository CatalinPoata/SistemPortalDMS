using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class AppointmentConfiguration
        : BaseEntityConfiguration<Appointment>
    {
        public override void Configure(
            EntityTypeBuilder<Appointment> builder)
        {
            base.Configure(builder);

            builder.ToTable("appointment", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_appointment_notes_length",
                    "notes IS NULL OR length(notes) <= 1000");

                table.HasCheckConstraint(
                    "CK_appointment_decision_note_length",
                    "decision_note IS NULL OR length(decision_note) <= 1000");

                table.HasCheckConstraint(
                    "CK_appointment_reference_code_length",
                    "length(reference_code) <= 20");

                table.HasEnumCheck<AppointmentStatus>(
                    "CK_appointment_status",
                    "status");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.HasOne(e => e.slot)
                .WithMany()
                .HasForeignKey(e => e.slot_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.user)
                .WithMany()
                .HasForeignKey(e => e.user_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.decided_by_user)
                .WithMany()
                .HasForeignKey(e => e.decided_by_user_id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);


            builder.Property(e => e.status)
                .HasVarcharEnum()
                .IsRequired()
                .HasDefaultValueSql("'Requested'");

            builder.Property(e => e.notes)
                .HasColumnType("text");

            builder.Property(e => e.decision_note)
                .HasColumnType("text");

            builder.Property(e => e.decided_at)
                .HasColumnType("timestamptz");

            builder.Property(e => e.reference_code)
                .HasColumnType("text")
                .IsRequired();

            builder.HasIndex(e => e.reference_code)
                .IsUnique();

            builder.HasIndex(e => new
            {
                e.slot_id,
                e.user_id
            })
                .IsUnique()
                .HasFilter("status IN ('Requested', 'Confirmed')");
        }
    }
}
