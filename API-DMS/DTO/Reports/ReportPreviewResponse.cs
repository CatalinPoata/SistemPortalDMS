namespace API_DMS.DTO.Reports
{
    public sealed record ReportPreviewResponse(
    IReadOnlyList<ReportPreviewColumnResponse> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    IReadOnlyDictionary<string, decimal> Totals,
    ReportPreviewMetaResponse Meta);

    public sealed record ReportPreviewColumnResponse(
        string Field,
        string Label,
        string Type,
        string Align,
        int WidthPct,
        string? Format);

    public sealed record ReportPreviewMetaResponse(
        int Page,
        int PageSize,
        int Total);
}
