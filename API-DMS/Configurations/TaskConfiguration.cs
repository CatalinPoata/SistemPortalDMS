using TaskEntity = API_DMS.Entities.Task;
using TaskStatus = API_DMS.Entities.TaskStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using API_DMS.Entities;

namespace API_DMS.Configurations
{
    public class TaskConfiguration : BaseEntityConfiguration<TaskEntity>
    {
        public override void Configure(EntityTypeBuilder<TaskEntity> builder)
        {
            base.Configure(builder);

            builder.ToTable("task", "dms", table =>
            {
                table.HasCheckConstraint(
                    "CK_task_assignee_or_department",
                    "assignee_user_id IS NOT NULL OR department_id IS NOT NULL");

                table.HasCheckConstraint(
                    "CK_task_title_length",
                    "length(title) <= 200");

                table.HasCheckConstraint(
                    "CK_task_resolution_note_length",
                    "resolution_note IS NULL OR length(resolution_note) <= 1000");

                table.HasEnumCheck<TaskStatus>(
                    "CK_task_status",
                    "status");
            });

            builder.HasKey(e => e.id);

            builder.Property(e => e.id)
                .ValueGeneratedOnAdd();

            builder.HasOne(e => e.entry)
                .WithMany()
                .HasForeignKey(e => e.entry_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.assignee_user)
                .WithMany()
                .HasForeignKey(e => e.assignee_user_id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(e => e.department)
                .WithMany()
                .HasForeignKey(e => e.department_id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(e => e.created_by_user)
                .WithMany()
                .HasForeignKey(e => e.created_by_user_id)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(e => e.title)
                .HasColumnType("text")
                .IsRequired();

            builder.Property(e => e.instructions)
                .HasColumnType("text");

            builder.Property(e => e.due_date)
                .HasColumnType("date");

            builder.Property(e => e.status)
                .HasVarcharEnum()
                .IsRequired()
                .HasDefaultValueSql("'Open'");

            builder.Property(e => e.resolution_note)
                .HasColumnType("text");

            builder.Property(e => e.completed_at)
                .HasColumnType("timestamptz");
        }
    }
}
