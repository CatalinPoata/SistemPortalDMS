using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Reporting
{
    public sealed record ReportDatasetDescriptor(
    string Key,
    string Label,
    IReadOnlyList<ReportFieldDescriptor> Fields,
    IReadOnlyList<ReportParameterDescriptor> Parameters);

    public sealed record ReportFieldDescriptor(
        string Key,
        string Label,
        string Type,
        bool IsNumeric);

    public sealed record ReportParameterDescriptor(
        string Name,
        string Label,
        string Type,
        string? Source);
}
