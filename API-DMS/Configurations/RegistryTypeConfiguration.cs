using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_DMS.Configurations
{
    public class RegistryTypeConfiguration : BaseEntityConfiguration<RegistryType>
    {
        public override void Configure(EntityTypeBuilder<RegistryType> builder)
        {
            base.Configure(builder);

            builder.ToTable("registry_type", "dms", table =>
            {
                table.HasCheckConstraint(
                    "CK_registry_type_code_length",
                    "length(code) <= 30");

                table.HasCheckConstraint(
                    "CK_registry_type_name_length",
                    "length(name) <= 200");

                table.HasEnumCheck<RegistryDirection>(
                    "CK_registry_type_direction",
                    "direction");

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

            builder.Property(e => e.direction)
                .HasVarcharEnum()
                .IsRequired();

            builder.Property(e => e.start_number)
                .IsRequired()
                .HasDefaultValue(1);

            builder.Property(e => e.default_deadline_days)
                .IsRequired()
                .HasDefaultValue(30);

            builder.Property(e => e.is_closed)
                .IsRequired()
                .HasDefaultValue(false);
        }
    }
}
