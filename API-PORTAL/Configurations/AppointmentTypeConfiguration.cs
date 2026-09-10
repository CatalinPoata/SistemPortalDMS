using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class AppointmentTypeConfiguration
        : BaseEntityConfiguration<AppointmentType>
    {
        public override void Configure(
            EntityTypeBuilder<AppointmentType> builder)
        {
            base.Configure(builder);

            builder.ToTable("appointment_type", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_appointment_type_code_length",
                    "length(code) <= 40");

                table.HasCheckConstraint(
                    "CK_appointment_type_name_length",
                    "length(name) <= 200");

                table.HasCheckConstraint(
                    "CK_appointment_type_description_length",
                    "description IS NULL OR length(description) <= 1000");

                table.HasCheckConstraint(
                    "CK_appointment_type_location_length",
                    "location IS NULL OR length(location) <= 250");

                table.HasCheckConstraint(
                    "CK_appointment_type_duration_minutes_positive",
                    "duration_minutes > 0");

                table.HasCheckConstraint(
                    "CK_appointment_type_max_days_ahead_nonnegative",
                    "max_days_ahead >= 0");
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

            builder.Property(e => e.description)
                .HasColumnType("text");

            builder.Property(e => e.location)
                .HasColumnType("text");

            builder.Property(e => e.duration_minutes)
                .IsRequired();

            builder.Property(e => e.requires_confirmation)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(e => e.max_days_ahead)
                .IsRequired()
                .HasDefaultValue(30);

            builder.Property(e => e.is_active)
                .IsRequired()
                .HasDefaultValue(true);
        }
    }
}
