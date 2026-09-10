using System.Text.Json;

namespace API_DMS.Integration
{
    public sealed class PortalRegistryEntryRequest
    {
        public Guid ExternalId { get; set; }
        public string? RegistryTypeCode { get; set; }
        public string? ServiceCode { get; set; }
        public string? Direction { get; set; }
        public string? Subject { get; set; }
        public PortalApplicant? Applicant { get; set; }
        public DateTimeOffset SubmittedAt { get; set; }
        public JsonElement FormValues { get; set; }
        public List<PortalDocument> Documents { get; set; } = [];
    }

    public sealed class PortalApplicant
    {
        public string? Name { get; set; }
        public string? NationalId { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
    }

    public sealed class PortalDocument
    {
        public Guid FileId { get; set; }
        public string? Name { get; set; }
        public string? ContentType { get; set; }
        public long SizeBytes { get; set; }
        public string? Sha256 { get; set; }
        public string? DocumentKindCode { get; set; }
    }

    public sealed record PortalRegistryEntryResponse(
        Guid EntryId,
        string RegistryTypeCode,
        string RegistryName,
        long Number,
        int Year,
        string DisplayNumber,
        DateTimeOffset RegisteredAt,
        string Status);

    public sealed class PortalClarificationRequest
    {
        public Guid ExternalId { get; set; }
        public Guid EntryId { get; set; }
        public List<PortalDocument> Documents { get; set; } = [];
    }

    public sealed record PortalClarificationResponse(
        Guid EntryId,
        string Status,
        IReadOnlyList<Guid> DocumentIds);

    public sealed class PortalCancellationRequest
    {
        public Guid ExternalId { get; set; }
        public Guid EntryId { get; set; }
        public string? Reason { get; set; }
    }

    public sealed record PortalCancellationResponse(
        Guid EntryId,
        string Status,
        DateTimeOffset CancelledAt);

    public sealed class PortalIntegrationRuleException : Exception
    {
        public PortalIntegrationRuleException(string message)
            : base(message)
        {
        }
    }
}
