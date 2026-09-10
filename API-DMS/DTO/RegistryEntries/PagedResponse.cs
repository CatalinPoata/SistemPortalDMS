namespace API_DMS.DTO.RegistryEntries
{
    public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total);
}
