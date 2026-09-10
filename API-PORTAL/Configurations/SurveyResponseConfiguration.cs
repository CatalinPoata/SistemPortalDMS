using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class SurveyResponseConfiguration : BaseEntityConfiguration<SurveyResponse>
    {
        public override void Configure(EntityTypeBuilder<SurveyResponse> builder)
        {
            base.Configure(builder);

            builder.ToTable("survey_response", "portal");

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.HasOne(e => e.survey)
                .WithMany()
                .HasForeignKey(e => e.survey_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.user)
                .WithMany()
                .HasForeignKey(e => e.user_id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Property(e => e.answers)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.Property(e => e.submitted_at)
                .HasColumnType("timestamptz")
                .IsRequired();

            builder.HasIndex(e => new
            {
                e.survey_id,
                e.user_id
            })
            .IsUnique()
            .HasFilter("user_id IS NOT NULL");
        }
    }
}
