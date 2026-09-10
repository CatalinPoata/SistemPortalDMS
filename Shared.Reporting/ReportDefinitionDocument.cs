using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Shared.Reporting
{
    public static class ReportJson
    {
        public static readonly JsonSerializerOptions Options =
            new(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling =
                    JsonUnmappedMemberHandling.Disallow
            };

        public static ReportDefinitionDocument Parse(
            JsonElement value)
        {
            return JsonSerializer.Deserialize<
                ReportDefinitionDocument>(
                value.GetRawText(),
                Options)
                ?? throw new JsonException(
                    "Definiția raportului este goală.");
        }
    }

    public sealed class ReportDefinitionDocument
    {
        public string RenderMode { get; init; } = "table";

        public string DatasetKey { get; init; } = string.Empty;

        public List<ReportParameterDefinition> Parameters { get; init; } = [];

        public List<ReportColumnDefinition> Columns { get; init; } = [];

        public List<ReportSortDefinition> Sort { get; init; } = [];

        public ReportGroupDefinition? GroupBy { get; init; }

        public List<ReportTotalDefinition> Totals { get; init; } = [];

        public ReportLayoutDefinition? Layout { get; init; }
    }

    public sealed class ReportParameterDefinition
    {
        public string Name { get; init; } = string.Empty;

        public string Type { get; init; } = string.Empty;

        public string? Source { get; init; }

        public string Label { get; init; } = string.Empty;

        public bool Required { get; init; }
    }

    public sealed class ReportColumnDefinition
    {
        public string Field { get; init; } = string.Empty;

        public string Label { get; init; } = string.Empty;

        public string Type { get; init; } = string.Empty;

        public string? Align { get; init; }

        public int WidthPct { get; init; }

        public string? Format { get; init; }
    }

    public sealed class ReportSortDefinition
    {
        public string Field { get; init; } = string.Empty;

        public string Dir { get; init; } = string.Empty;
    }

    public sealed class ReportGroupDefinition
    {
        public string Field { get; init; } = string.Empty;
    }

    public sealed class ReportTotalDefinition
    {
        public string Field { get; init; } = string.Empty;

        public string Agg { get; init; } = string.Empty;
    }

    public sealed class ReportLayoutDefinition
    {
        public string Orientation { get; init; } = "portrait";

        public string Title { get; init; } = string.Empty;

        public string? Subtitle { get; init; }

        public bool ShowPageNumbers { get; init; } = true;
    }
}
