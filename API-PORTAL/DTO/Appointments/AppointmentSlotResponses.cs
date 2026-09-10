namespace API_PORTAL.DTO.Appointments
{
    public sealed record AppointmentSlotResponse(
        Guid Id,
        Guid AppointmentTypeId,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        int Capacity,
        int BookedCount,
        bool IsBlocked);

    public sealed record PagedAppointmentSlotResponse(
        IReadOnlyList<AppointmentSlotResponse> Items,
        int Page,
        int PageSize,
        int Total);
}
