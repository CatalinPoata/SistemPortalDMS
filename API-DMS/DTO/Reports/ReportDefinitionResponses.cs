using System.Text.Json;

namespace API_DMS.DTO.Reports
{
    public sealed record ReportDefinitionSummaryResponse(
    Guid Id,
    string Code,
    string Name,
    string DatasetKey,
    int Version,
    bool IsSystem,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

    public sealed record ReportDefinitionDetailsResponse(
        Guid Id,
        string Code,
        string Name,
        string DatasetKey,
        JsonElement Definition,
        int Version,
        bool IsSystem,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    public sealed record ReportDatasetResponse(
    string Key,
    string Label,
    IReadOnlyList<ReportDatasetFieldResponse> Fields,
    IReadOnlyList<ReportDatasetParameterResponse> Parameters);

    public sealed record ReportDatasetFieldResponse(
        string Key,
        string Label,
        string Type,
        bool IsNumeric);

    public sealed record ReportDatasetParameterResponse(
        string Name,
        string Label,
        string Type,
        string? Source);
}
