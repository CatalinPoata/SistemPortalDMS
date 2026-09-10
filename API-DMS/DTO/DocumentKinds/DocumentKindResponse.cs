namespace API_DMS.DTO.DocumentKinds
{
    public sealed record DocumentKindResponse(
        Guid Id,
        string Code,
        string Name,
        bool IsActive);
}
