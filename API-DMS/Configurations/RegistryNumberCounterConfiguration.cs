using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_DMS.Configurations
{
    public class RegistryNumberCounterConfiguration : BaseEntityConfiguration<RegistryNumberCounter>
    {
        public override void Configure(EntityTypeBuilder<RegistryNumberCounter> builder)
        {
            base.Configure(builder);
            builder.ToTable("registry_number_counter", "dms");

            builder.HasKey(e => new { e.registry_type_id, e.year });

            builder.HasOne(e => e.registry_type)
                .WithMany()
                .HasForeignKey(e => e.registry_type_id)
                .IsRequired();
        }
    }
}
