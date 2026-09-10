using API_DMS.Entities;

namespace API_DMS.DTO.Integration
{
    public sealed record IntegrationRegistryTypeResponse(
        Guid Id,
        string Code,
        string Name,
        RegistryDirection Direction,
        bool IsClosed);
}
