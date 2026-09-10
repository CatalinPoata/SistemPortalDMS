using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public class ArticleConfiguration : BaseEntityConfiguration<Article>
    {
        public override void Configure(EntityTypeBuilder<Article> builder)
        {
            base.Configure(builder);

            builder.ToTable("article", "portal", table =>
            {
                table.HasCheckConstraint(
                    "CK_article_slug_length",
                    "length(slug) <= 160");

                table.HasCheckConstraint(
                    "CK_article_slug_format",
                    "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");

                table.HasCheckConstraint(
                    "CK_article_title_length",
                    "length(title) <= 250");

                table.HasCheckConstraint(
                    "CK_article_summary_length",
                    "summary IS NULL OR length(summary) <= 500");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.Property(e => e.slug)
                .HasColumnType("text")
                .IsRequired();

            builder.HasIndex(e => e.slug)
                .IsUnique();

            builder.Property(e => e.title)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.summary)
                .HasColumnType("text");

            builder.Property(e => e.body)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.published_at)
                .HasColumnType("timestamptz");

            builder.Property(e => e.is_published)
                .IsRequired()
                .HasDefaultValue(false);
        }
    }
}
