using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace API_PORTAL.DTO.Reports;

public sealed class CreateReportDefinitionRequest
{
    [Required]
    [StringLength(50)]
    public string? Code { get; set; }

    [Required]
    [StringLength(200)]
    public string? Name { get; set; }

    [Required]
    [StringLength(50)]
    public string? DatasetKey { get; set; }

    [Required]
    public JsonElement? Definition { get; set; }
}

public sealed class UpdateReportDefinitionRequest
{
    [Required]
    [StringLength(200)]
    public string? Name { get; set; }

    [Required]
    [StringLength(50)]
    public string? DatasetKey { get; set; }

    [Required]
    public JsonElement? Definition { get; set; }

    [Range(1, int.MaxValue)]
    public int ExpectedVersion { get; set; }
}

public sealed class PreviewReportDefinitionRequest
{
    [Required]
    [StringLength(50)]
    public string? DatasetKey { get; set; }

    [Required]
    public JsonElement? Definition { get; set; }
}
