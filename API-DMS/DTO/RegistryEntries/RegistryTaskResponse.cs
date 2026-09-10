using TaskStatus = API_DMS.Entities.TaskStatus;

namespace API_DMS.DTO.RegistryEntries
{
    public sealed record RegistryTaskResponse(
    Guid Id,
    Guid EntryId,
    Guid? AssigneeUserId,
    string? AssigneeEmail,
    Guid? DepartmentId,
    string? DepartmentCode,
    string? DepartmentName,
    string Title,
    string? Instructions,
    DateOnly? DueDate,
    TaskStatus Status,
    string? ResolutionNote,
    DateTimeOffset? CompletedAt,
    Guid CreatedByUserId,
    DateTime CreatedAt);
}
