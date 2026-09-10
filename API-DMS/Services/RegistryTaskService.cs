using API_DMS.Data;
using API_DMS.Entities;
using API_DMS.Entities.Base;
using API_DMS.DTO.RegistryEntries;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaskEntity = API_DMS.Entities.Task;
using TaskStatus = API_DMS.Entities.TaskStatus;

namespace API_DMS.Services
{
    public sealed class RegistryTaskRuleException : Exception
    {
        public RegistryTaskRuleException(string message)
            : base(message)
        {
        }
    }

    public sealed class RegistryTaskService
    {
        private readonly DmsDbContext db;

        public RegistryTaskService(DmsDbContext db)
        {
            this.db = db;
        }

        public async Task<RegistryTaskResponse?> CreateAsync(
            Guid entryId,
            CreateRegistryTaskRequest request,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var entry = await db.RegistryEntries
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.id == entryId,
                    cancellationToken);

            if (entry is null)
            {
                return null;
            }

            if (entry.status is
                EntryStatus.Completed or
                EntryStatus.Rejected or
                EntryStatus.Cancelled)
            {
                throw new RegistryTaskRuleException(
                    "Nu se poate crea un task pentru o poziție finalizată.");
            }

            if (!request.AssigneeUserId.HasValue &&
                !request.DepartmentId.HasValue)
            {
                throw new RegistryTaskRuleException(
                    "Task-ul trebuie repartizat către un utilizator sau un compartiment.");
            }

            var title = request.Title?.Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new RegistryTaskRuleException(
                    "Titlul task-ului este obligatoriu.");
            }

            if (title.Length > 200)
            {
                throw new RegistryTaskRuleException(
                    "Titlul task-ului nu poate depăși 200 de caractere.");
            }

            var instructions = string.IsNullOrWhiteSpace(
                request.Instructions)
                ? null
                : request.Instructions.Trim();

            var today = DateOnly.FromDateTime(
                DateTime.UtcNow);

            if (request.DueDate.HasValue &&
                request.DueDate.Value < today)
            {
                throw new RegistryTaskRuleException(
                    "Termenul task-ului nu poate fi anterior datei curente.");
            }

            User? assignee = null;

            if (request.AssigneeUserId.HasValue)
            {
                assignee = await db.Users
                    .SingleOrDefaultAsync(
                        x => x.id == request.AssigneeUserId.Value &&
                             x.is_active,
                        cancellationToken);

                if (assignee is null)
                {
                    throw new RegistryTaskRuleException(
                        "Utilizatorul repartizat nu există sau este inactiv.");
                }

                if (assignee.role is not (Role.Clerk or Role.Admin))
                {
                    throw new RegistryTaskRuleException(
                        "Task-ul poate fi repartizat doar unui Clerk sau Admin.");
                }
            }

            Department? department = null;

            if (request.DepartmentId.HasValue)
            {
                department = await db.Departments
                    .SingleOrDefaultAsync(
                        x => x.id == request.DepartmentId.Value &&
                             x.is_active,
                        cancellationToken);

                if (department is null)
                {
                    throw new RegistryTaskRuleException(
                        "Compartimentul nu există sau este inactiv.");
                }
            }

            var task = new TaskEntity
            {
                id = Guid.NewGuid(),
                entry_id = entryId,
                assignee_user_id = assignee?.id,
                department_id = department?.id,
                created_by_user_id = actorUserId,
                title = title,
                instructions = instructions,
                due_date = request.DueDate,
                status = TaskStatus.Open
            };

            var occurredAt = DateTimeOffset.UtcNow;

            db.Tasks.Add(task);

            db.EntryEvents.Add(new EntryEvent
            {
                id = Guid.NewGuid(),
                entry_id = entryId,
                occurred_at = occurredAt,
                actor_user_id = actorUserId,
                type = EventType.Assigned,
                message = "Poziția a fost repartizată.",
                payload = JsonSerializer.SerializeToDocument(new
                {
                    taskId = task.id,
                    assigneeUserId = task.assignee_user_id,
                    departmentId = task.department_id,
                    dueDate = task.due_date
                })
            });

            await db.SaveChangesAsync(cancellationToken);

            return ToResponse(task, assignee, department);
        }

        public async Task<RegistryTaskResponse?> CompleteAsync(
            Guid entryId,
            Guid taskId,
            CompleteRegistryTaskRequest request,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var task = await db.Tasks
                .Include(x => x.assignee_user)
                .Include(x => x.department)
                .SingleOrDefaultAsync(
                    x => x.id == taskId &&
                         x.entry_id == entryId,
                    cancellationToken);

            if (task is null)
            {
                return null;
            }

            if (task.status != TaskStatus.Open)
            {
                throw new RegistryTaskRuleException(
                    "Doar task-urile deschise pot fi marcate ca rezolvate.");
            }

            var resolutionNote = request.ResolutionNote?.Trim();

            if (string.IsNullOrWhiteSpace(resolutionNote))
            {
                throw new RegistryTaskRuleException(
                    "Nota de rezolvare este obligatorie.");
            }

            if (resolutionNote.Length > 1000)
            {
                throw new RegistryTaskRuleException(
                    "Nota de rezolvare nu poate depăși 1000 de caractere.");
            }

            var completedAt = DateTimeOffset.UtcNow;

            task.status = TaskStatus.Done;
            task.resolution_note = resolutionNote;
            task.completed_at = completedAt;

            db.EntryEvents.Add(new EntryEvent
            {
                id = Guid.NewGuid(),
                entry_id = entryId,
                occurred_at = completedAt,
                actor_user_id = actorUserId,
                type = EventType.TaskCompleted,
                message = "Task-ul a fost marcat ca rezolvat.",
                payload = JsonSerializer.SerializeToDocument(new
                {
                    taskId,
                    resolutionNote
                })
            });

            await db.SaveChangesAsync(cancellationToken);

            return ToResponse(
                task,
                task.assignee_user,
                task.department);
        }

        private static RegistryTaskResponse ToResponse(
            TaskEntity task,
            User? assignee,
            Department? department)
        {
            return new RegistryTaskResponse(
                task.id,
                task.entry_id,
                task.assignee_user_id,
                assignee?.email,
                task.department_id,
                department?.code,
                department?.name,
                task.title,
                task.instructions,
                task.due_date,
                task.status,
                task.resolution_note,
                task.completed_at,
                task.created_by_user_id,
                task.created_at);
        }
    }
}
