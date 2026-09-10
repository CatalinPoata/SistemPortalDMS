using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class SurveyQuestionConfiguration
        : BaseEntityConfiguration<SurveyQuestion>
    {
        public override void Configure(
            EntityTypeBuilder<SurveyQuestion> builder)
        {
            base.Configure(builder);

            builder.ToTable("survey_question", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_survey_question_key_length",
                    "length(key) <= 60");

                table.HasCheckConstraint(
                    "CK_survey_question_text_length",
                    "length(text) <= 1000");

                table.HasCheckConstraint(
                    "CK_survey_question_display_order_non_negative",
                    "display_order >= 0");

                table.HasEnumCheck<SurveyQuestionType>(
                    "CK_survey_question_type",
                    "type");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.HasOne(e => e.survey)
                .WithMany()
                .HasForeignKey(e => e.survey_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new
            {
                e.survey_id,
                e.key
            })
            .IsUnique();

            builder.HasIndex(e => new
            {
                e.survey_id,
                e.display_order
            });

            builder.Property(e => e.key)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.text)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.type)
                .HasVarcharEnum()
                .IsRequired();

            builder.Property(e => e.options)
                .HasColumnType("jsonb");

            builder.Property(e => e.is_required)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(e => e.display_order)
                .IsRequired()
                .HasDefaultValue(0);
        }
    }
}
