using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class SurveyConfiguration : BaseEntityConfiguration<Survey>
    {
        public override void Configure(EntityTypeBuilder<Survey> builder)
        {
            base.Configure(builder);

            builder.ToTable("survey", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_survey_code_length",
                    "length(code) <= 50");

                table.HasCheckConstraint(
                    "CK_survey_title_length",
                    "length(title) <= 250");

                table.HasCheckConstraint(
                    "CK_survey_dates",
                    "ends_at IS NULL OR starts_at IS NULL OR ends_at > starts_at");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.Property(e => e.code)
                .HasColumnType("text")
                .IsRequired();

            builder.HasIndex(e => e.code)
                .IsUnique();

            builder.Property(e => e.title)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.description)
                .HasColumnType("text");

            builder.Property(e => e.starts_at)
                .HasColumnType("timestamptz");

            builder.Property(e => e.ends_at)
                .HasColumnType("timestamptz");

            builder.Property(e => e.allow_anonymous)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(e => e.show_results)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(e => e.is_published)
                .IsRequired()
                .HasDefaultValue(false);
        }
    }
}
