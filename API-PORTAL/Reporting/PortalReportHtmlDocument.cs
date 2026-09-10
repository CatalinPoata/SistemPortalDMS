using API_PORTAL.DTO.Reports;
using API_PORTAL.Entities;
using Microsoft.AspNetCore.Http;
using Shared.Reporting;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace API_PORTAL.Reporting;

public sealed record RenderedPortalReportHtml(string Html, bool ShowPageNumbers);

public static class PortalReportHtmlDocument
{
    private static readonly CultureInfo RomanianCulture = CultureInfo.GetCultureInfo("ro-RO");
    private static readonly Regex PlaceholderPattern = new(
        "\\{([A-Za-z][A-Za-z0-9_]*)\\}",
        RegexOptions.CultureInvariant);

    public static RenderedPortalReportHtml Create(
        ReportDefinition report,
        ReportPreviewResponse preview,
        IQueryCollection query)
    {
        var definition = ReportJson.Parse(report.definition.RootElement);
        var layout = definition.Layout ?? throw new InvalidOperationException(
            "Definiția raportului nu are layout.");
        var title = ExpandTemplate(layout.Title, definition.Parameters, query) ?? report.name;
        var subtitle = ExpandTemplate(layout.Subtitle, definition.Parameters, query);
        var body = definition.RenderMode == "record" ? BuildRecord(preview) : BuildTable(preview);

        return new RenderedPortalReportHtml(
            BuildHtml(title, subtitle, layout.Orientation, body),
            layout.ShowPageNumbers);
    }

    private static string BuildHtml(
        string title,
        string? subtitle,
        string orientation,
        string body)
    {
        var safeOrientation = orientation == "landscape" ? "landscape" : "portrait";
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeSubtitle = string.IsNullOrWhiteSpace(subtitle)
            ? string.Empty
            : $"<p class=\"subtitle\">{WebUtility.HtmlEncode(subtitle)}</p>";

        return $$"""
        <!DOCTYPE html>
        <html lang="ro">
        <head>
            <meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy"
                  content="default-src 'none'; style-src 'unsafe-inline'">
            <title>{{safeTitle}}</title>
            <style>
                @page { size: A4 {{safeOrientation}}; margin: 14mm 12mm 18mm; }
                * { box-sizing: border-box; }
                body { margin: 0; color: #0f172a; font: 9.5pt "DejaVu Sans", sans-serif; }
                h1 { margin: 0 0 4mm; font-size: 16pt; }
                .subtitle { margin: 0 0 6mm; color: #475569; }
                table { width: 100%; border-collapse: collapse; table-layout: fixed; }
                thead { display: table-header-group; }
                tr { break-inside: avoid; }
                th, td { padding: 6px; border: 1px solid #94a3b8; vertical-align: top; overflow-wrap: anywhere; }
                th { background: #e2e8f0; font-weight: 700; }
                .left { text-align: left; }
                .center { text-align: center; }
                .right { text-align: right; }
                .empty { padding: 12px; color: #475569; text-align: center; }
                .totals { margin-top: 5mm; font-size: 9pt; }
                .totals ul { margin: 2mm 0 0; padding-left: 5mm; }
                .record { display: grid; grid-template-columns: minmax(35%, 42%) 1fr; margin: 0; border-top: 1px solid #94a3b8; border-left: 1px solid #94a3b8; }
                .record dt, .record dd { min-height: 9mm; margin: 0; padding: 6px; border-right: 1px solid #94a3b8; border-bottom: 1px solid #94a3b8; overflow-wrap: anywhere; }
                .record dt { background: #e2e8f0; font-weight: 700; }
            </style>
        </head>
        <body>
            <h1>{{safeTitle}}</h1>
            {{safeSubtitle}}
            {{body}}
        </body>
        </html>
        """;
    }

    private static string BuildTable(ReportPreviewResponse preview)
    {
        var columns = new StringBuilder();
        var headers = new StringBuilder();
        var rows = new StringBuilder();

        foreach (var column in preview.Columns)
        {
            columns.Append($"<col style=\"width:{column.WidthPct}%\">");
            headers.Append($"<th class=\"{column.Align}\">{WebUtility.HtmlEncode(column.Label)}</th>");
        }

        foreach (var row in preview.Rows)
        {
            rows.Append("<tr>");
            foreach (var column in preview.Columns)
            {
                row.TryGetValue(column.Field, out var value);
                rows.Append($"<td class=\"{column.Align}\">{WebUtility.HtmlEncode(FormatValue(value, column))}</td>");
            }

            rows.Append("</tr>");
        }

        if (preview.Rows.Count == 0)
        {
            rows.Append($"<tr><td class=\"empty\" colspan=\"{preview.Columns.Count}\">Nu există date pentru parametrii selectați.</td></tr>");
        }

        return $$"""
        <table>
            <colgroup>{{columns}}</colgroup>
            <thead><tr>{{headers}}</tr></thead>
            <tbody>{{rows}}</tbody>
        </table>
        {{BuildTotals(preview.Totals)}}
        """;
    }

    private static string BuildRecord(ReportPreviewResponse preview)
    {
        var row = preview.Rows.FirstOrDefault();
        if (row is null)
        {
            return "<p class=\"empty\">Nu există date pentru parametrii selectați.</p>";
        }

        var fields = new StringBuilder();
        foreach (var column in preview.Columns)
        {
            row.TryGetValue(column.Field, out var value);
            fields.Append($"<dt class=\"{column.Align}\">{WebUtility.HtmlEncode(column.Label)}</dt>");
            fields.Append($"<dd class=\"{column.Align}\">{WebUtility.HtmlEncode(FormatValue(value, column))}</dd>");
        }

        return $"<dl class=\"record\">{fields}</dl>";
    }

    private static string BuildTotals(IReadOnlyDictionary<string, decimal> totals)
    {
        if (totals.Count == 0)
        {
            return string.Empty;
        }

        var values = string.Join(string.Empty, totals.OrderBy(item => item.Key).Select(item =>
            $"<li>{WebUtility.HtmlEncode(item.Key)}: {item.Value.ToString("N2", RomanianCulture)}</li>"));
        return $"<section class=\"totals\"><strong>Totaluri</strong><ul>{values}</ul></section>";
    }

    private static string FormatValue(object? value, ReportPreviewColumnResponse column)
    {
        if (value is null)
        {
            return "—";
        }

        return value switch
        {
            DateOnly date => FormatDate(date, column.Format),
            DateTimeOffset instant => FormatDateTime(instant, column.Format),
            JsonElement json => json.ValueKind == JsonValueKind.String
                ? json.GetString() ?? string.Empty
                : json.GetRawText(),
            IFormattable formattable => FormatFormattable(formattable, column.Format),
            _ => Convert.ToString(value, RomanianCulture) ?? string.Empty
        };
    }

    private static string FormatDate(DateOnly value, string? format)
    {
        try { return value.ToString(string.IsNullOrWhiteSpace(format) ? "dd.MM.yyyy" : format, RomanianCulture); }
        catch (FormatException) { return value.ToString("dd.MM.yyyy", RomanianCulture); }
    }

    private static string FormatDateTime(DateTimeOffset value, string? format)
    {
        try { return value.ToString(string.IsNullOrWhiteSpace(format) ? "dd.MM.yyyy HH:mm" : format, RomanianCulture); }
        catch (FormatException) { return value.ToString("dd.MM.yyyy HH:mm", RomanianCulture); }
    }

    private static string FormatFormattable(IFormattable value, string? format)
    {
        try { return value.ToString(format, RomanianCulture) ?? string.Empty; }
        catch (FormatException) { return value.ToString(null, RomanianCulture) ?? string.Empty; }
    }

    private static string? ExpandTemplate(
        string? value,
        IReadOnlyList<ReportParameterDefinition> parameters,
        IQueryCollection query)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var declared = parameters.Select(parameter => parameter.Name).ToHashSet(StringComparer.Ordinal);
        return PlaceholderPattern.Replace(value, match =>
        {
            var name = match.Groups[1].Value;
            return declared.Contains(name) && query.TryGetValue(name, out var supplied) && supplied.Count == 1
                ? supplied[0] ?? "—"
                : "—";
        });
    }
}
