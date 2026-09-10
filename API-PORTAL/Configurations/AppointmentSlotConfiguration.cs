using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class AppointmentSlotConfiguration
        : BaseEntityConfiguration<AppointmentSlot>
    {
        public override void Configure(
            EntityTypeBuilder<AppointmentSlot> builder)
        {
            base.Configure(builder);

            builder.ToTable("appointment_slot", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_appointment_slot_ends_after_starts",
                    "ends_at > starts_at");

                table.HasCheckConstraint(
                    "CK_appointment_slot_capacity_positive",
                    "capacity > 0");

                table.HasCheckConstraint(
                    "CK_appointment_slot_booked_count_valid",
                    "booked_count >= 0 AND booked_count <= capacity");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.HasOne(e => e.appointment_type)
                .WithMany()
                .HasForeignKey(e => e.appointment_type_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new
            {
                e.appointment_type_id,
                e.starts_at
            })
            .IsUnique();

            builder.Property(e => e.starts_at)
                .HasColumnType("timestamptz")
                .IsRequired();

            builder.Property(e => e.ends_at)
                .HasColumnType("timestamptz")
                .IsRequired();

            builder.Property(e => e.capacity)
                .IsRequired()
                .HasDefaultValue(1);

            builder.Property(e => e.booked_count)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(e => e.is_blocked)
                .IsRequired()
                .HasDefaultValue(false);
        }
    }
}
