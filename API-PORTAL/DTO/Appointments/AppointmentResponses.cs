using API_PORTAL.Entities;

namespace API_PORTAL.DTO.Appointments
{
    public sealed record AppointmentResponse(
        Guid Id,
        Guid SlotId,
        string AppointmentTypeCode,
        string AppointmentTypeName,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        AppointmentStatus Status,
        string? Notes,
        string? DecisionNote,
        string ReferenceCode);

    public sealed record PagedAppointmentResponse(
        IReadOnlyList<AppointmentResponse> Items,
        int Page,
        int PageSize,
        int Total);
}
