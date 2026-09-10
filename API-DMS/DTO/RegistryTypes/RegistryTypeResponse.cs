using API_DMS.Entities;

namespace API_DMS.DTO.RegistryTypes
{
    public sealed record RegistryTypeResponse(
    Guid Id,
    string Code,
    string Name,
    RegistryDirection Direction,
    long StartNumber,
    int DefaultDeadlineDays,
    bool IsClosed);
}
