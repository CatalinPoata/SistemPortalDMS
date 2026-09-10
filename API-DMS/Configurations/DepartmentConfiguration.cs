using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_DMS.Configurations
{
    public class DepartmentConfiguration : BaseEntityConfiguration<Department>
    {
        public override void Configure(EntityTypeBuilder<Department> builder)
        {
            base.Configure(builder);

            builder.ToTable("department", "dms", table =>
            {
                table.HasCheckConstraint(
                    "CK_department_code_length",
                    "length(code) <= 20");

                table.HasCheckConstraint(
                    "CK_department_name_length",
                    "length(name) <= 200");
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

            builder.HasOne(e => e.manager_user)
                .WithMany()
                .HasForeignKey(e => e.manager_user_id)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Property(e => e.is_active)
                .IsRequired()
                .HasDefaultValue(true);
        }
    }
}
