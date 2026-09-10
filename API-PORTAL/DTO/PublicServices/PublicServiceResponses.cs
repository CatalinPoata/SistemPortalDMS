using System.Text.Json;

namespace API_PORTAL.DTO.PublicServices
{
    public sealed record PublicServiceListItemResponse(
        Guid Id,
        string Code,
        string Title,
        string? ShortDescription,
        bool RequiresAttachment,
        int MaxAttachments);

    public sealed record PublicServiceDetailsResponse(
        Guid Id,
        string Code,
        string Title,
        string? ShortDescription,
        string? Description,
        JsonElement FormSchema,
        int SchemaVersion,
        bool RequiresAttachment,
        int MaxAttachments);
}
