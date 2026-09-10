using System.Text.Json;

namespace API_PORTAL.DTO.ServiceDefinitions
{
    public sealed record ServiceDefinitionListItemResponse(
        Guid Id,
        string Code,
        string Title,
        string? ShortDescription,
        string RegistryTypeCode,
        int SchemaVersion,
        bool RequiresAttachment,
        int MaxAttachments,
        bool IsPublished,
        int DisplayOrder,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    public sealed record ServiceDefinitionDetailsResponse(
        Guid Id,
        string Code,
        string Title,
        string? ShortDescription,
        string? Description,
        string RegistryTypeCode,
        JsonElement FormSchema,
        int SchemaVersion,
        bool RequiresAttachment,
        int MaxAttachments,
        bool IsPublished,
        int DisplayOrder,
        int SubmissionCount,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int Page,
        int PageSize,
        int Total);
}
