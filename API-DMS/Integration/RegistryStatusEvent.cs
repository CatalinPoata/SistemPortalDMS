namespace API_DMS.Integration
{
    public sealed record RegistryStatusEvent(
        Guid EventId,
        Guid EntryId,
        Guid ExternalId,
        string Status,
        string Message,
        string? StatusNote,
        DateTimeOffset OccurredAt,
        RegistryResponseDocument? ResponseDocument);

    public sealed record RegistryResponseDocument(
        Guid DocumentId,
        string Name,
        string ContentType,
        long SizeBytes,
        string Sha256);
}
