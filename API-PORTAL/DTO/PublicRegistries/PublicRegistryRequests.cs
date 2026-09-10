using System.ComponentModel.DataAnnotations;

namespace API_PORTAL.DTO.PublicRegistries;

public class CreatePublicRegistryRequest
{
    [Required, StringLength(40)]
    public string? Code { get; set; }

    [Required, StringLength(200)]
    public string? Name { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }
}

public sealed class UpdatePublicRegistryRequest : CreatePublicRegistryRequest
{
    public bool IsPublished { get; set; }
}

public class CreatePublicRegistryEntryRequest
{
    [Required, StringLength(40)]
    public string? PositionNumber { get; set; }

    [Required, StringLength(500)]
    public string? Title { get; set; }

    [Required]
    public DateOnly EntryDate { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }
}

public sealed class UpdatePublicRegistryEntryRequest : CreatePublicRegistryEntryRequest
{
    public bool IsPublished { get; set; }
}

public sealed class UploadPublicRegistryDocumentRequest
{
    [Required]
    public IFormFile? File { get; set; }

    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }
}
