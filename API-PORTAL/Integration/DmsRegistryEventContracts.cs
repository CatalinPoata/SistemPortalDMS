namespace API_PORTAL.Integration
{
    public sealed class DmsRegistryEventCallback
    {
        public Guid EventId { get; set; }
        public Guid EntryId { get; set; }
        public Guid ExternalId { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
        public string? StatusNote { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
        public DmsResponseDocumentMetadata? ResponseDocument { get; set; }
    }

    public sealed class DmsResponseDocumentMetadata
    {
        public Guid DocumentId { get; set; }
        public string? Name { get; set; }
        public string? ContentType { get; set; }
        public long SizeBytes { get; set; }
        public string? Sha256 { get; set; }
    }
}
