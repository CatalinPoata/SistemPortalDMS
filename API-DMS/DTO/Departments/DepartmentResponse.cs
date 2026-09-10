namespace API_DMS.DTO.Departments
{
    public sealed record DepartmentResponse(
        Guid Id,
        string Code,
        string Name,
        Guid? ManagerUserId,
        string? ManagerEmail,
        bool IsActive);
}
