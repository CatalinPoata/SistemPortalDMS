using System.Globalization;
using System.Text;

namespace Shared.Reporting;

public static class CsvReportDocument
{
    public static byte[] Create<TColumn>(
        IEnumerable<TColumn> columns,
        IEnumerable<IReadOnlyDictionary<string, object?>> rows,
        Func<TColumn, string> field,
        Func<TColumn, string> label)
    {
        var selectedColumns = columns.ToArray();
        var csv = new StringBuilder();

        csv.Append('\uFEFF');
        AppendRow(csv, selectedColumns.Select(label));

        foreach (var row in rows)
        {
            AppendRow(csv, selectedColumns.Select(column =>
            {
                row.TryGetValue(field(column), out var value);
                return Format(value);
            }));
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            .GetBytes(csv.ToString());
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string> values)
    {
        csv.AppendJoin(',', values.Select(Escape));
        csv.Append("\r\n");
    }

    private static string Escape(string value)
    {
        var requiresQuotes = value.IndexOfAny([',', '"', '\r', '\n']) >= 0;
        var escaped = value.Replace("\"", "\"\"");
        return requiresQuotes ? $"\"{escaped}\"" : escaped;
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTimeOffset dateTime => dateTime.ToUniversalTime()
            .ToString("O", CultureInfo.InvariantCulture),
        DateTime dateTime => dateTime.ToUniversalTime()
            .ToString("O", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        double number => number.ToString(CultureInfo.InvariantCulture),
        float number => number.ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };
}
