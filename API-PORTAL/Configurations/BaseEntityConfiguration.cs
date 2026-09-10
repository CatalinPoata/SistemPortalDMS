using API_PORTAL.Entities.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations
{
    public abstract class BaseEntityConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity
    {
        public virtual void Configure(EntityTypeBuilder<T> builder)
        {

            builder.Property(e => e.created_at)
                .IsRequired()
                .HasColumnType("timestamptz");

            builder.Property(e => e.updated_at)
                .HasColumnType("timestamptz");
        }
    }
}
