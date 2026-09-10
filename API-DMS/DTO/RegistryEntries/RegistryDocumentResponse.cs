using API_DMS.Entities;

namespace API_DMS.DTO.RegistryEntries
{
    public sealed record RegistryDocumentResponse(
    Guid Id,
    Guid EntryId,
    Guid? ExternalFileId,
    DocumentDirection Direction,
    Guid DocumentKindId,
    string DocumentKindCode,
    string DocumentKindName,
    DateOnly? DocumentDate,
    string? Issuer,
    string? Note,
    string OriginalName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    Guid UploadedByUserId);
}
