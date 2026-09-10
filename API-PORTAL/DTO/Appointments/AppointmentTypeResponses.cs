namespace API_PORTAL.DTO.Appointments
{
    public sealed record AppointmentTypeResponse(
        Guid Id,
        string Code,
        string Name,
        string? Description,
        string? Location,
        int DurationMinutes,
        bool RequiresConfirmation,
        int MaxDaysAhead,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    public sealed record PagedAppointmentTypeResponse(
        IReadOnlyList<AppointmentTypeResponse> Items,
        int Page,
        int PageSize,
        int Total);
}
