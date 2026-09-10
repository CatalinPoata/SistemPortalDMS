using API_PORTAL.Data;
using API_PORTAL.DTO.Reports;
using API_PORTAL.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Shared.Reporting;
using System.Globalization;
using System.Linq.Expressions;

namespace API_PORTAL.Reporting;

public sealed class PortalReportPreviewValidationException
    : Exception
{
    public PortalReportPreviewValidationException(
        IReadOnlyDictionary<string, string[]> errors)
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

public sealed class PortalReportPreviewService
{
    private const int MaximumPdfRows = 5_000;

    private readonly PortalDbContext db;
    private readonly PortalReportDefinitionValidator validator;

    public PortalReportPreviewService(
        PortalDbContext db,
        PortalReportDefinitionValidator validator)
    {
        this.db = db;
        this.validator = validator;
    }

    public Task<ReportPreviewResponse> PreviewAsync(
        ReportDefinition report,
        IQueryCollection query,
        CancellationToken cancellationToken) => RunAsync(
            report,
            query,
            exportAllRows: false,
            cancellationToken);

    public Task<ReportPreviewResponse> ExportAsync(
        ReportDefinition report,
        IQueryCollection query,
        CancellationToken cancellationToken) => RunAsync(
            report,
            query,
            exportAllRows: true,
            cancellationToken);

    private async Task<ReportPreviewResponse> RunAsync(
        ReportDefinition report,
        IQueryCollection query,
        bool exportAllRows,
        CancellationToken cancellationToken)
    {
        var definitionErrors = validator.Validate(
            report.dataset_key,
            report.definition.RootElement);
        if (definitionErrors.Count > 0)
        {
            throw new PortalReportPreviewValidationException(definitionErrors);
        }

        var definition = ReportJson.Parse(report.definition.RootElement);
        var parameters = await ParseQueryAsync(
            definition,
            report.dataset_key,
            query,
            cancellationToken);

        if (exportAllRows)
        {
            parameters = parameters with { Page = 1, PageSize = MaximumPdfRows };
        }

        return report.dataset_key switch
        {
            "submissions" => await PreviewSubmissionsAsync(
                definition,
                parameters,
                cancellationToken,
                exportAllRows ? MaximumPdfRows : null),
            "appointments" => await PreviewAppointmentsAsync(
                definition,
                parameters,
                cancellationToken,
                exportAllRows ? MaximumPdfRows : null),
            _ => throw new PortalReportPreviewValidationException(
                new Dictionary<string, string[]>
                {
                    ["datasetKey"] = ["Setul de date nu este suportat."]
                })
        };
    }

    private async Task<ParsedReportQuery> ParseQueryAsync(
        ReportDefinitionDocument definition,
        string datasetKey,
        IQueryCollection query,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Add(string key, string message)
        {
            if (!errors.TryGetValue(key, out var values))
            {
                values = [];
                errors[key] = values;
            }

            values.Add(message);
        }

        var declared = definition.Parameters.ToDictionary(item => item.Name, StringComparer.Ordinal);
        foreach (var key in query.Keys)
        {
            if (key is not ("page" or "pageSize") && !declared.ContainsKey(key))
            {
                Add(key, "Parametrul nu este declarat în definiția raportului.");
            }
        }

        var page = ReadBoundedInteger(query, "page", 1, 1, 1_000_000, Add);
        var pageSize = ReadBoundedInteger(query, "pageSize", 25, 1, 100, Add);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var parameter in definition.Parameters)
        {
            if (!query.TryGetValue(parameter.Name, out var supplied))
            {
                if (parameter.Required)
                {
                    Add(parameter.Name, "Parametrul este obligatoriu.");
                }

                continue;
            }

            if (supplied.Count != 1 || string.IsNullOrWhiteSpace(supplied[0]))
            {
                Add(parameter.Name, "Parametrul trebuie transmis o singură dată.");
                continue;
            }

            var value = await ParseParameterAsync(
                datasetKey,
                parameter,
                supplied,
                Add,
                cancellationToken);
            if (value is not null)
            {
                values[parameter.Name] = value;
            }
        }

        if (values.TryGetValue("dateFrom", out var rawFrom) &&
            values.TryGetValue("dateTo", out var rawTo) &&
            rawFrom is DateOnly from && rawTo is DateOnly to && from > to)
        {
            Add("dateTo", "Data de sfârșit trebuie să fie ulterioară datei de început.");
        }

        if (errors.Count > 0)
        {
            throw new PortalReportPreviewValidationException(errors.ToDictionary(
                item => item.Key,
                item => item.Value.ToArray(),
                StringComparer.Ordinal));
        }

        return new ParsedReportQuery(page, pageSize, values);
    }

    private async Task<object?> ParseParameterAsync(
        string datasetKey,
        ReportParameterDefinition parameter,
        StringValues supplied,
        Action<string, string> add,
        CancellationToken cancellationToken)
    {
        var raw = supplied[0]!;
        if (parameter.Type == "date")
        {
            if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }

            add(parameter.Name, "Data trebuie să aibă formatul AAAA-LL-ZZ.");
            return null;
        }

        if (parameter.Type == "lookup")
        {
            if (parameter.Source == "statuses")
            {
                if (datasetKey == "submissions" && Enum.TryParse<SubmissionStatus>(raw, false, out var submissionStatus))
                {
                    return submissionStatus;
                }

                if (datasetKey == "appointments" && Enum.TryParse<AppointmentStatus>(raw, false, out var appointmentStatus))
                {
                    return appointmentStatus;
                }

                add(parameter.Name, "Starea transmisă nu este validă pentru acest set de date.");
                return null;
            }

            if (!Guid.TryParse(raw, out var id))
            {
                add(parameter.Name, "Valoarea lookup trebuie să fie un UUID valid.");
                return null;
            }

            var exists = parameter.Source switch
            {
                "service_definitions" => await db.ServiceDefinitions.AnyAsync(item => item.id == id, cancellationToken),
                "appointment_types" => await db.AppointmentTypes.AnyAsync(item => item.id == id, cancellationToken),
                _ => false
            };

            if (!exists)
            {
                add(parameter.Name, "Valoarea lookup nu există.");
                return null;
            }

            return id;
        }

        add(parameter.Name, "Tipul parametrului nu este suportat.");
        return null;
    }

    private async Task<ReportPreviewResponse> PreviewSubmissionsAsync(
        ReportDefinitionDocument definition,
        ParsedReportQuery parameters,
        CancellationToken cancellationToken,
        int? maximumRows)
    {
        var query = ApplySubmissionFilters(db.Submissions.AsNoTracking(), parameters.Values);
        query = ApplySubmissionOrdering(query, definition);
        var total = await query.CountAsync(cancellationToken);
        var totals = await CalculateSubmissionTotalsAsync(
            query,
            definition.Totals,
            total,
            cancellationToken);
        var pageSize = maximumRows ?? parameters.PageSize;
        var rows = await query
            .Skip((parameters.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SubmissionSourceRow(
                item.external_id,
                item.service.code,
                item.service.title,
                item.schema_version,
                item.user.full_name,
                item.user.email,
                item.submitted_at,
                item.registered_at,
                item.registry_display_number,
                item.status,
                item.status_details))
            .ToListAsync(cancellationToken);

        return BuildResponse(
            definition,
            rows,
            BuildSubmissionRow,
            total,
            totals,
            parameters,
            pageSize);
    }

    private async Task<ReportPreviewResponse> PreviewAppointmentsAsync(
        ReportDefinitionDocument definition,
        ParsedReportQuery parameters,
        CancellationToken cancellationToken,
        int? maximumRows)
    {
        var query = ApplyAppointmentFilters(db.Appointments.AsNoTracking(), parameters.Values);
        query = ApplyAppointmentOrdering(query, definition);
        var total = await query.CountAsync(cancellationToken);
        var totals = await CalculateAppointmentTotalsAsync(
            query,
            definition.Totals,
            total,
            cancellationToken);
        var pageSize = maximumRows ?? parameters.PageSize;
        var rows = await query
            .Skip((parameters.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AppointmentSourceRow(
                item.reference_code,
                item.slot.appointment_type.code,
                item.slot.appointment_type.name,
                item.slot.starts_at,
                item.slot.ends_at,
                item.slot.capacity,
                item.slot.booked_count,
                item.user.full_name,
                item.user.email,
                item.status,
                item.decision_note))
            .ToListAsync(cancellationToken);

        return BuildResponse(
            definition,
            rows,
            BuildAppointmentRow,
            total,
            totals,
            parameters,
            pageSize);
    }

    private static ReportPreviewResponse BuildResponse<T>(
        ReportDefinitionDocument definition,
        IEnumerable<T> sourceRows,
        Func<T, IReadOnlyList<ReportColumnDefinition>, IReadOnlyDictionary<string, object?>> buildRow,
        int total,
        IReadOnlyDictionary<string, decimal> totals,
        ParsedReportQuery parameters,
        int pageSize)
    {
        var columns = definition.Columns.Select(column => new ReportPreviewColumnResponse(
            column.Field,
            column.Label,
            column.Type,
            column.Align ?? DefaultAlignment(column.Type),
            column.WidthPct,
            column.Format)).ToList();

        var rows = sourceRows.Select(row => buildRow(row, definition.Columns)).ToList();
        return new ReportPreviewResponse(
            columns,
            rows,
            totals,
            new ReportPreviewMetaResponse(parameters.Page, pageSize, total));
    }

    private static async Task<IReadOnlyDictionary<string, decimal>>
        CalculateSubmissionTotalsAsync(
            IQueryable<Submission> query,
            IReadOnlyList<ReportTotalDefinition> definitions,
            int rowCount,
            CancellationToken cancellationToken)
    {
        var totals = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            var key = $"{definition.Agg}:{definition.Field}";
            totals[key] = definition.Agg switch
            {
                "count" => rowCount,
                _ when rowCount == 0 => 0m,
                "sum" when definition.Field == "schema_version" =>
                    await query.SumAsync(item => (decimal)item.schema_version, cancellationToken),
                "avg" when definition.Field == "schema_version" =>
                    await query.AverageAsync(item => (decimal)item.schema_version, cancellationToken),
                _ => 0m
            };
        }

        return totals;
    }

    private static async Task<IReadOnlyDictionary<string, decimal>>
        CalculateAppointmentTotalsAsync(
            IQueryable<Appointment> query,
            IReadOnlyList<ReportTotalDefinition> definitions,
            int rowCount,
            CancellationToken cancellationToken)
    {
        var totals = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            var key = $"{definition.Agg}:{definition.Field}";
            totals[key] = definition.Agg switch
            {
                "count" => rowCount,
                _ when rowCount == 0 => 0m,
                "sum" when definition.Field == "slot_capacity" =>
                    await query.SumAsync(item => (decimal)item.slot.capacity, cancellationToken),
                "avg" when definition.Field == "slot_capacity" =>
                    await query.AverageAsync(item => (decimal)item.slot.capacity, cancellationToken),
                "sum" when definition.Field == "slot_booked_count" =>
                    await query.SumAsync(item => (decimal)item.slot.booked_count, cancellationToken),
                "avg" when definition.Field == "slot_booked_count" =>
                    await query.AverageAsync(item => (decimal)item.slot.booked_count, cancellationToken),
                _ => 0m
            };
        }

        return totals;
    }

    private static IQueryable<Submission> ApplySubmissionFilters(
        IQueryable<Submission> query,
        IReadOnlyDictionary<string, object?> values)
    {
        if (values.TryGetValue("serviceId", out var service) && service is Guid serviceId)
        {
            query = query.Where(item => item.service_id == serviceId);
        }

        if (values.TryGetValue("status", out var state) && state is SubmissionStatus status)
        {
            query = query.Where(item => item.status == status);
        }

        if (values.TryGetValue("dateFrom", out var from) && from is DateOnly fromDate)
        {
            query = query.Where(item => item.submitted_at >= StartOfDay(fromDate));
        }

        if (values.TryGetValue("dateTo", out var to) && to is DateOnly toDate)
        {
            query = query.Where(item => item.submitted_at < StartOfDay(toDate.AddDays(1)));
        }

        return query;
    }

    private static IQueryable<Appointment> ApplyAppointmentFilters(
        IQueryable<Appointment> query,
        IReadOnlyDictionary<string, object?> values)
    {
        if (values.TryGetValue("appointmentTypeId", out var type) && type is Guid typeId)
        {
            query = query.Where(item => item.slot.appointment_type_id == typeId);
        }

        if (values.TryGetValue("status", out var state) && state is AppointmentStatus status)
        {
            query = query.Where(item => item.status == status);
        }

        if (values.TryGetValue("dateFrom", out var from) && from is DateOnly fromDate)
        {
            query = query.Where(item => item.slot.starts_at >= StartOfDay(fromDate));
        }

        if (values.TryGetValue("dateTo", out var to) && to is DateOnly toDate)
        {
            query = query.Where(item => item.slot.starts_at < StartOfDay(toDate.AddDays(1)));
        }

        return query;
    }

    private static IOrderedQueryable<Submission> ApplySubmissionOrdering(
        IQueryable<Submission> query,
        ReportDefinitionDocument definition)
    {
        var sort = EffectiveSort(definition);
        IOrderedQueryable<Submission>? ordered = null;
        foreach (var item in sort)
        {
            var desc = item.Dir == "desc";
            ordered = item.Field switch
            {
                "external_id" => Order(query, ordered, row => row.external_id, desc),
                "service_code" => Order(query, ordered, row => row.service.code, desc),
                "service_title" => Order(query, ordered, row => row.service.title, desc),
                "schema_version" => Order(query, ordered, row => row.schema_version, desc),
                "applicant_name" => Order(query, ordered, row => row.user.full_name, desc),
                "applicant_email" => Order(query, ordered, row => row.user.email, desc),
                "submitted_at" => Order(query, ordered, row => row.submitted_at, desc),
                "registered_at" => Order(query, ordered, row => row.registered_at, desc),
                "registry_display_number" => Order(query, ordered, row => row.registry_display_number ?? "", desc),
                "status" => Order(query, ordered, row => row.status, desc),
                "status_details" => Order(query, ordered, row => row.status_details ?? "", desc),
                _ => ordered
            };
        }

        return ordered ?? query.OrderByDescending(item => item.submitted_at);
    }

    private static IOrderedQueryable<Appointment> ApplyAppointmentOrdering(
        IQueryable<Appointment> query,
        ReportDefinitionDocument definition)
    {
        var sort = EffectiveSort(definition);
        IOrderedQueryable<Appointment>? ordered = null;
        foreach (var item in sort)
        {
            var desc = item.Dir == "desc";
            ordered = item.Field switch
            {
                "reference_code" => Order(query, ordered, row => row.reference_code, desc),
                "appointment_type_code" => Order(query, ordered, row => row.slot.appointment_type.code, desc),
                "appointment_type_name" => Order(query, ordered, row => row.slot.appointment_type.name, desc),
                "starts_at" => Order(query, ordered, row => row.slot.starts_at, desc),
                "ends_at" => Order(query, ordered, row => row.slot.ends_at, desc),
                "slot_capacity" => Order(query, ordered, row => row.slot.capacity, desc),
                "slot_booked_count" => Order(query, ordered, row => row.slot.booked_count, desc),
                "applicant_name" => Order(query, ordered, row => row.user.full_name, desc),
                "applicant_email" => Order(query, ordered, row => row.user.email, desc),
                "status" => Order(query, ordered, row => row.status, desc),
                "decision_note" => Order(query, ordered, row => row.decision_note ?? "", desc),
                _ => ordered
            };
        }

        return ordered ?? query.OrderBy(item => item.slot.starts_at);
    }

    private static IReadOnlyList<ReportSortDefinition> EffectiveSort(ReportDefinitionDocument definition)
    {
        var sort = new List<ReportSortDefinition>();
        if (!string.IsNullOrWhiteSpace(definition.GroupBy?.Field))
        {
            sort.Add(new ReportSortDefinition { Field = definition.GroupBy.Field, Dir = "asc" });
        }

        sort.AddRange(definition.Sort.Where(item => !sort.Any(existing => existing.Field == item.Field)));
        return sort;
    }

    private static IOrderedQueryable<T> Order<T, TKey>(
        IQueryable<T> query,
        IOrderedQueryable<T>? ordered,
        Expression<Func<T, TKey>> selector,
        bool descending) => ordered is null
            ? (descending ? query.OrderByDescending(selector) : query.OrderBy(selector))
            : (descending ? ordered.ThenByDescending(selector) : ordered.ThenBy(selector));

    private static IReadOnlyDictionary<string, object?> BuildSubmissionRow(
        SubmissionSourceRow row,
        IReadOnlyList<ReportColumnDefinition> columns)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["external_id"] = row.ExternalId,
            ["service_code"] = row.ServiceCode,
            ["service_title"] = row.ServiceTitle,
            ["schema_version"] = row.SchemaVersion,
            ["applicant_name"] = row.ApplicantName,
            ["applicant_email"] = row.ApplicantEmail,
            ["submitted_at"] = row.SubmittedAt,
            ["registered_at"] = row.RegisteredAt,
            ["registry_display_number"] = row.RegistryDisplayNumber,
            ["status"] = row.Status.ToString(),
            ["status_details"] = row.StatusDetails
        };

        return columns.ToDictionary(column => column.Field, column => values[column.Field], StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, object?> BuildAppointmentRow(
        AppointmentSourceRow row,
        IReadOnlyList<ReportColumnDefinition> columns)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["reference_code"] = row.ReferenceCode,
            ["appointment_type_code"] = row.TypeCode,
            ["appointment_type_name"] = row.TypeName,
            ["starts_at"] = row.StartsAt,
            ["ends_at"] = row.EndsAt,
            ["slot_capacity"] = row.SlotCapacity,
            ["slot_booked_count"] = row.SlotBookedCount,
            ["applicant_name"] = row.ApplicantName,
            ["applicant_email"] = row.ApplicantEmail,
            ["status"] = row.Status.ToString(),
            ["decision_note"] = row.DecisionNote
        };

        return columns.ToDictionary(column => column.Field, column => values[column.Field], StringComparer.Ordinal);
    }

    private static int ReadBoundedInteger(
        IQueryCollection query,
        string key,
        int defaultValue,
        int minimum,
        int maximum,
        Action<string, string> add)
    {
        if (!query.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value.Count != 1 || !int.TryParse(value[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < minimum || parsed > maximum)
        {
            add(key, $"Valoarea trebuie să fie între {minimum} și {maximum}.");
            return defaultValue;
        }

        return parsed;
    }

    private static DateTimeOffset StartOfDay(DateOnly value) => new(
        value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    private static string DefaultAlignment(string type) => type is "number" or "date" or "code" ? "center" : "left";

    private sealed record ParsedReportQuery(
        int Page,
        int PageSize,
        IReadOnlyDictionary<string, object?> Values);

    private sealed record SubmissionSourceRow(
        Guid ExternalId,
        string ServiceCode,
        string ServiceTitle,
        int SchemaVersion,
        string ApplicantName,
        string ApplicantEmail,
        DateTimeOffset SubmittedAt,
        DateTimeOffset? RegisteredAt,
        string? RegistryDisplayNumber,
        SubmissionStatus Status,
        string? StatusDetails);

    private sealed record AppointmentSourceRow(
        string ReferenceCode,
        string TypeCode,
        string TypeName,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        int SlotCapacity,
        int SlotBookedCount,
        string ApplicantName,
        string ApplicantEmail,
        AppointmentStatus Status,
        string? DecisionNote);
}
