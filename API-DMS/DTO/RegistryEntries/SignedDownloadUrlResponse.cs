namespace API_DMS.DTO.RegistryEntries
{
    public sealed record SignedDownloadUrlResponse(
        Guid DocumentId,
        string Url,
        DateTimeOffset ExpiresAt);
}
