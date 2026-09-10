using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class NotificationConfiguration : BaseEntityConfiguration<Notification>
    {
        public override void Configure(EntityTypeBuilder<Notification> builder)
        {
            base.Configure(builder);

            builder.ToTable("notification", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_notification_subject_length",
                    "length(subject) <= 200");

                table.HasCheckConstraint(
                    "CK_notification_link_url_length",
                    "length(link_url) <= 500");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.HasOne(e => e.user)
                .WithMany()
                .HasForeignKey(e => e.user_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => e.user_id);

            builder.Property(e => e.subject)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.body)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.link_url)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.read_at)
                .HasColumnType("timestamptz");

            builder.Property(e => e.sent_at)
                .HasColumnType("timestamptz");
        }
    }
}
