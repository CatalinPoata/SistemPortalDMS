namespace API_PORTAL.DTO.PublicRegistries;

public sealed record PublicRegistryDocumentResponse(
    Guid Id,
    string OriginalName,
    string ContentType,
    long SizeBytes,
    int DisplayOrder,
    string DownloadUrl);

public sealed record PublicRegistryEntryResponse(
    Guid Id,
    string PositionNumber,
    string Title,
    DateOnly EntryDate,
    string? Description,
    bool IsPublished,
    IReadOnlyList<PublicRegistryDocumentResponse> Documents);

public sealed record PublicRegistryResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsPublished,
    int DisplayOrder,
    IReadOnlyList<PublicRegistryEntryResponse> Entries);

public sealed record PublicRegistryListItemResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsPublished,
    int DisplayOrder,
    int EntryCount);

public sealed record PagedPublicRegistryResponse(
    IReadOnlyList<PublicRegistryListItemResponse> Items,
    int Page,
    int PageSize,
    int Total);
