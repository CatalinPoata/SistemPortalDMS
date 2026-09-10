using API_PORTAL.Entities;
using System.Text.Json;

namespace API_PORTAL.Integration
{
    public sealed record PortalSubmissionRegistrationPayload(
        Guid ExternalId,
        string RegistryTypeCode,
        string ServiceCode,
        string Direction,
        string Subject,
        PortalSubmissionApplicant Applicant,
        DateTimeOffset SubmittedAt,
        JsonElement FormValues,
        IReadOnlyList<PortalSubmissionDocument> Documents)
    {
        public static PortalSubmissionRegistrationPayload Create(
            Submission submission,
            ServiceDefinition service,
            User user,
            IEnumerable<SubmissionFile> files)
        {
            return new PortalSubmissionRegistrationPayload(
                submission.external_id,
                service.registry_type_code,
                service.code,
                "In",
                service.title,
                new PortalSubmissionApplicant(
                    user.full_name,
                    user.national_id,
                    user.email,
                    user.phone,
                    user.address),
                submission.submitted_at,
                submission.values.RootElement.Clone(),
                files.Select(file => new PortalSubmissionDocument(
                    file.id,
                    file.original_name,
                    file.content_type,
                    file.size_bytes,
                    file.sha256,
                    "ANEXA")).ToArray());
        }
    }

    public sealed record PortalSubmissionApplicant(
        string Name,
        string? NationalId,
        string Email,
        string? Phone,
        string? Address);

    public sealed record PortalSubmissionDocument(
        Guid FileId,
        string Name,
        string ContentType,
        long SizeBytes,
        string Sha256,
        string DocumentKindCode);

    public sealed record DmsRegistrationResponse(
        Guid EntryId,
        string RegistryTypeCode,
        string RegistryName,
        long Number,
        int Year,
        string DisplayNumber,
        DateTimeOffset RegisteredAt,
        string Status);

    public sealed record PortalSubmissionClarificationPayload(
        Guid ExternalId,
        Guid EntryId,
        IReadOnlyList<PortalSubmissionDocument> Documents);

    public sealed record PortalSubmissionCancellationPayload(
        Guid ExternalId,
        Guid EntryId,
        string Reason);
}
