using API_DMS.Data;
using API_DMS.DTO.Reports;
using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Shared.Reporting;
using System.Globalization;
using System.Linq.Expressions;
using WorkflowTask = API_DMS.Entities.Task;
using WorkflowTaskStatus = API_DMS.Entities.TaskStatus;

namespace API_DMS.Reports
{
    public sealed class ReportPreviewValidationException : Exception
    {
        public ReportPreviewValidationException(
            IReadOnlyDictionary<string, string[]> errors)
        {
            Errors = errors;
        }

        public IReadOnlyDictionary<string, string[]> Errors { get; }
    }

    public sealed class DmsReportPreviewService
    {
        private const int MaximumPdfRows = 5_000;

        private readonly DmsDbContext db;
        private readonly DmsReportDefinitionValidator validator;

        public DmsReportPreviewService(
            DmsDbContext db,
            DmsReportDefinitionValidator validator)
        {
            this.db = db;
            this.validator = validator;
        }

        public Task<ReportPreviewResponse> PreviewAsync(
            ReportDefinition report,
            IQueryCollection query,
            CancellationToken cancellationToken)
        {
            return RunAsync(
                report,
                query,
                exportAllRows: false,
                cancellationToken);
        }

        public Task<ReportPreviewResponse> ExportAsync(
            ReportDefinition report,
            IQueryCollection query,
            CancellationToken cancellationToken)
        {
            return RunAsync(
                report,
                query,
                exportAllRows: true,
                cancellationToken);
        }

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
                throw new ReportPreviewValidationException(
                    definitionErrors);
            }

            var definition = ReportJson.Parse(
                report.definition.RootElement);

            var parameters = await ParseQueryAsync(
                definition,
                report.dataset_key,
                query,
                cancellationToken);

            if (exportAllRows)
            {
                parameters = parameters with
                {
                    Page = 1,
                    PageSize = MaximumPdfRows
                };
            }

            return report.dataset_key switch
            {
                "registry_entries" => await PreviewRegistryEntriesAsync(
                    definition,
                    parameters,
                    cancellationToken,
                    exportAllRows ? MaximumPdfRows : null),

                "tasks" => await PreviewTasksAsync(
                    definition,
                    parameters,
                    cancellationToken,
                    exportAllRows ? MaximumPdfRows : null),

                _ => throw new ReportPreviewValidationException(
                    new Dictionary<string, string[]>
                    {
                        ["datasetKey"] =
                        [
                            $"Dataset-ul '{report.dataset_key}' nu este suportat."
                        ]
                    })
            };
        }

        private async Task<ParsedReportQuery> ParseQueryAsync(
            ReportDefinitionDocument definition,
            string datasetKey,
            IQueryCollection query,
            CancellationToken cancellationToken)
        {
            var errors = new Dictionary<string, List<string>>(
                StringComparer.Ordinal);

            void Add(string key, string message)
            {
                if (!errors.TryGetValue(key, out var messages))
                {
                    messages = [];
                    errors[key] = messages;
                }

                messages.Add(message);
            }

            var declaredParameters = definition.Parameters
                .ToDictionary(
                    parameter => parameter.Name,
                    StringComparer.Ordinal);

            foreach (var key in query.Keys)
            {
                if (key is "page" or "pageSize")
                {
                    continue;
                }

                if (!declaredParameters.ContainsKey(key))
                {
                    Add(
                        key,
                        "Parametrul nu este declarat în definiția raportului.");
                }
            }

            var page = ReadBoundedInteger(
                query,
                "page",
                1,
                1,
                1_000_000,
                Add);

            var pageSize = ReadBoundedInteger(
                query,
                "pageSize",
                25,
                1,
                100,
                Add);

            var values = new Dictionary<string, object?>(
                StringComparer.Ordinal);

            foreach (var parameter in definition.Parameters)
            {
                if (!query.TryGetValue(
                        parameter.Name,
                        out var suppliedValue))
                {
                    if (parameter.Required)
                    {
                        Add(
                            parameter.Name,
                            "Parametrul este obligatoriu.");
                    }

                    continue;
                }

                if (suppliedValue.Count != 1 ||
                    string.IsNullOrWhiteSpace(suppliedValue[0]))
                {
                    Add(
                        parameter.Name,
                        "Parametrul trebuie transmis o singură dată.");

                    continue;
                }

                var parsed = await ParseParameterAsync(
                    datasetKey,
                    parameter,
                    suppliedValue,
                    Add,
                    cancellationToken);

                if (parsed is not null)
                {
                    values[parameter.Name] = parsed;
                }
            }

            if (values.TryGetValue("dateFrom", out var fromValue) &&
                values.TryGetValue("dateTo", out var toValue) &&
                fromValue is DateOnly fromDate &&
                toValue is DateOnly toDate &&
                fromDate > toDate)
            {
                Add(
                    "dateTo",
                    "Data de sfârșit trebuie să fie ulterioară datei de început.");
            }

            if (errors.Count > 0)
            {
                throw new ReportPreviewValidationException(
                    errors.ToDictionary(
                        item => item.Key,
                        item => item.Value.ToArray()));
            }

            return new ParsedReportQuery(
                page,
                pageSize,
                values);
        }

        private async Task<object?> ParseParameterAsync(
            string datasetKey,
            ReportParameterDefinition parameter,
            StringValues suppliedValue,
            Action<string, string> add,
            CancellationToken cancellationToken)
        {
            var raw = suppliedValue[0]!;

            switch (parameter.Type)
            {
                case "date":
                    if (DateOnly.TryParseExact(
                            raw,
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var date))
                    {
                        return date;
                    }

                    add(
                        parameter.Name,
                        "Data trebuie să aibă formatul AAAA-LL-ZZ.");

                    return null;

                case "int":
                    if (int.TryParse(
                            raw,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var number) &&
                        number > 0)
                    {
                        return number;
                    }

                    add(
                        parameter.Name,
                        "Valoarea trebuie să fie un număr întreg pozitiv.");

                    return null;

                case "bool":
                    if (bool.TryParse(raw, out var booleanValue))
                    {
                        return booleanValue;
                    }

                    add(
                        parameter.Name,
                        "Valoarea trebuie să fie true sau false.");

                    return null;

                case "string":
                    if (datasetKey == "registry_entries" &&
                        parameter.Name == "entryId")
                    {
                        return await ParseRegistryEntryIdAsync(
                            parameter,
                            raw,
                            add,
                            cancellationToken);
                    }

                    return raw.Trim();

                case "lookup":
                    return await ParseLookupAsync(
                        datasetKey,
                        parameter,
                        raw,
                        add,
                        cancellationToken);

                default:
                    add(
                        parameter.Name,
                        "Tipul parametrului nu este suportat.");

                    return null;
            }
        }

        private async Task<object?> ParseLookupAsync(
            string datasetKey,
            ReportParameterDefinition parameter,
            string raw,
            Action<string, string> add,
            CancellationToken cancellationToken)
        {
            if (parameter.Source == "statuses")
            {
                if (datasetKey == "registry_entries" &&
                    Enum.TryParse<EntryStatus>(
                        raw,
                        ignoreCase: false,
                        out var entryStatus))
                {
                    return entryStatus;
                }

                if (datasetKey == "tasks" &&
                    Enum.TryParse<WorkflowTaskStatus>(
                        raw,
                        ignoreCase: false,
                        out var taskStatus))
                {
                    return taskStatus;
                }

                add(
                    parameter.Name,
                    "Starea transmisă nu este validă pentru acest set de date.");

                return null;
            }

            if (!Guid.TryParse(raw, out var id))
            {
                add(
                    parameter.Name,
                    "Valoarea lookup trebuie să fie un UUID valid.");

                return null;
            }

            var exists = parameter.Source switch
            {
                "registry_types" => await db.RegistryTypes
                    .AnyAsync(item => item.id == id, cancellationToken),

                "departments" => await db.Departments
                    .AnyAsync(item => item.id == id, cancellationToken),

                "document_kinds" => await db.DocumentKinds
                    .AnyAsync(item => item.id == id, cancellationToken),

                _ => false
            };

            if (!exists)
            {
                add(
                    parameter.Name,
                    "Valoarea lookup nu există.");

                return null;
            }

            return id;
        }

        private async Task<object?> ParseRegistryEntryIdAsync(
            ReportParameterDefinition parameter,
            string raw,
            Action<string, string> add,
            CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(raw, out var entryId))
            {
                add(
                    parameter.Name,
                    "ID-ul poziției trebuie să fie un UUID valid.");

                return null;
            }

            var exists = await db.RegistryEntries.AnyAsync(
                entry => entry.id == entryId,
                cancellationToken);

            if (!exists)
            {
                add(
                    parameter.Name,
                    "Poziția indicată nu există.");

                return null;
            }

            return entryId;
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

            if (value.Count != 1 ||
                !int.TryParse(
                    value[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var result) ||
                result < minimum ||
                result > maximum)
            {
                add(
                    key,
                    $"Valoarea trebuie să fie între {minimum} și {maximum}.");

                return defaultValue;
            }

            return result;
        }

        private static IQueryable<RegistryEntry> ApplyFilters(
            IQueryable<RegistryEntry> entries,
            IReadOnlyDictionary<string, object?> values)
        {
            if (values.TryGetValue(
                    "entryId",
                    out var entryValue) &&
                entryValue is Guid entryId)
            {
                entries = entries.Where(entry =>
                    entry.id == entryId);
            }

            if (values.TryGetValue(
                    "registryTypeId",
                    out var registryValue) &&
                registryValue is Guid registryTypeId)
            {
                entries = entries.Where(entry =>
                    entry.registry_type_id == registryTypeId);
            }

            if (values.TryGetValue(
                    "departmentId",
                    out var departmentValue) &&
                departmentValue is Guid departmentId)
            {
                entries = entries.Where(entry =>
                    entry.department_id == departmentId);
            }

            if (values.TryGetValue(
                    "status",
                    out var statusValue) &&
                statusValue is EntryStatus status)
            {
                entries = entries.Where(entry =>
                    entry.status == status);
            }

            if (values.TryGetValue(
                    "year",
                    out var yearValue) &&
                yearValue is int year)
            {
                entries = entries.Where(entry =>
                    entry.year == year);
            }

            if (values.TryGetValue(
                    "dateFrom",
                    out var fromValue) &&
                fromValue is DateOnly fromDate)
            {
                entries = entries.Where(entry =>
                    entry.registered_at >= ToUtcStart(fromDate));
            }

            if (values.TryGetValue(
                    "dateTo",
                    out var toValue) &&
                toValue is DateOnly toDate)
            {
                entries = entries.Where(entry =>
                    entry.registered_at < ToUtcStart(
                        toDate.AddDays(1)));
            }

            return entries;
        }

        private static IQueryable<WorkflowTask> ApplyTaskFilters(
            IQueryable<WorkflowTask> tasks,
            IReadOnlyDictionary<string, object?> values)
        {
            if (values.TryGetValue(
                    "departmentId",
                    out var departmentValue) &&
                departmentValue is Guid departmentId)
            {
                tasks = tasks.Where(task =>
                    task.department_id == departmentId);
            }

            if (values.TryGetValue(
                    "status",
                    out var statusValue) &&
                statusValue is WorkflowTaskStatus status)
            {
                tasks = tasks.Where(task =>
                    task.status == status);
            }

            if (values.TryGetValue(
                    "dateFrom",
                    out var fromValue) &&
                fromValue is DateOnly fromDate)
            {
                tasks = tasks.Where(task =>
                    task.due_date.HasValue &&
                    task.due_date >= fromDate);
            }

            if (values.TryGetValue(
                    "dateTo",
                    out var toValue) &&
                toValue is DateOnly toDate)
            {
                tasks = tasks.Where(task =>
                    task.due_date.HasValue &&
                    task.due_date <= toDate);
            }

            return tasks;
        }

        private static IOrderedQueryable<RegistryEntry> ApplyOrdering(
            IQueryable<RegistryEntry> entries,
            ReportDefinitionDocument definition)
        {
            var sort = new List<ReportSortDefinition>();

            if (!string.IsNullOrWhiteSpace(
                    definition.GroupBy?.Field))
            {
                sort.Add(new ReportSortDefinition
                {
                    Field = definition.GroupBy.Field,
                    Dir = "asc"
                });
            }

            sort.AddRange(definition.Sort
                .Where(item => !sort.Any(existing =>
                    existing.Field == item.Field)));

            if (sort.Count == 0)
            {
                sort.Add(new ReportSortDefinition
                {
                    Field = "registered_at",
                    Dir = "desc"
                });
            }

            IOrderedQueryable<RegistryEntry>? ordered = null;

            foreach (var item in sort)
            {
                var descending = item.Dir == "desc";

                ordered = item.Field switch
                {
                    "number" => Order(
                        entries,
                        ordered,
                        entry => entry.number,
                        descending),

                    "year" => Order(
                        entries,
                        ordered,
                        entry => entry.year,
                        descending),

                    "display_number" => Order(
                        entries,
                        ordered,
                        entry => entry.number,
                        descending),

                    "registry_type_code" => Order(
                        entries,
                        ordered,
                        entry => entry.registry_type.code,
                        descending),

                    "registry_type_name" => Order(
                        entries,
                        ordered,
                        entry => entry.registry_type.name,
                        descending),

                    "direction" => Order(
                        entries,
                        ordered,
                        entry => entry.direction,
                        descending),

                    "registered_at" => Order(
                        entries,
                        ordered,
                        entry => entry.registered_at,
                        descending),

                    "deadline" => Order(
                        entries,
                        ordered,
                        entry => entry.deadline,
                        descending),

                    "applicant_name" => Order(
                        entries,
                        ordered,
                        entry => entry.applicant_name,
                        descending),

                    "subject" => Order(
                        entries,
                        ordered,
                        entry => entry.subject,
                        descending),

                    "department_code" => Order(
                        entries,
                        ordered,
                        entry => entry.department == null
                            ? ""
                            : entry.department.code,
                        descending),

                    "department_name" => Order(
                        entries,
                        ordered,
                        entry => entry.department == null
                            ? ""
                            : entry.department.name,
                        descending),

                    "status" => Order(
                        entries,
                        ordered,
                        entry => entry.status,
                        descending),

                    _ => ordered
                };
            }

            return ordered ?? entries.OrderByDescending(
                entry => entry.registered_at);
        }

        private static IOrderedQueryable<WorkflowTask> ApplyTaskOrdering(
            IQueryable<WorkflowTask> tasks,
            ReportDefinitionDocument definition)
        {
            var sort = new List<ReportSortDefinition>();

            if (!string.IsNullOrWhiteSpace(
                    definition.GroupBy?.Field))
            {
                sort.Add(new ReportSortDefinition
                {
                    Field = definition.GroupBy.Field,
                    Dir = "asc"
                });
            }

            sort.AddRange(definition.Sort
                .Where(item => !sort.Any(existing =>
                    existing.Field == item.Field)));

            IOrderedQueryable<WorkflowTask>? ordered = null;

            foreach (var item in sort)
            {
                var descending = item.Dir == "desc";

                ordered = item.Field switch
                {
                    "title" => OrderTask(
                        tasks,
                        ordered,
                        task => task.title,
                        descending),

                    "status" => OrderTask(
                        tasks,
                        ordered,
                        task => task.status,
                        descending),

                    "due_date" => OrderTask(
                        tasks,
                        ordered,
                        task => task.due_date,
                        descending),

                    "completed_at" => OrderTask(
                        tasks,
                        ordered,
                        task => task.completed_at,
                        descending),

                    "assignee_email" => OrderTask(
                        tasks,
                        ordered,
                        task => task.assignee_user == null
                            ? ""
                            : task.assignee_user.email,
                        descending),

                    "department_code" => OrderTask(
                        tasks,
                        ordered,
                        task => task.department == null
                            ? ""
                            : task.department.code,
                        descending),

                    "entry_display_number" => OrderTask(
                        tasks,
                        ordered,
                        task => task.entry.number,
                        descending),

                    "entry_subject" => OrderTask(
                        tasks,
                        ordered,
                        task => task.entry.subject,
                        descending),

                    _ => throw new ReportPreviewValidationException(
                        new Dictionary<string, string[]>
                        {
                            ["sort"] =
                            [
                                $"Câmpul '{item.Field}' nu poate fi folosit pentru sortare."
                            ]
                        })
                };
            }

            return ordered ?? tasks
                .OrderBy(task => task.due_date == null)
                .ThenBy(task => task.due_date)
                .ThenBy(task => task.title);
        }

        private static IOrderedQueryable<RegistryEntry> Order<TKey>(
            IQueryable<RegistryEntry> source,
            IOrderedQueryable<RegistryEntry>? ordered,
            Expression<Func<RegistryEntry, TKey>> selector,
            bool descending)
        {
            if (ordered is null)
            {
                return descending
                    ? source.OrderByDescending(selector)
                    : source.OrderBy(selector);
            }

            return descending
                ? ordered.ThenByDescending(selector)
                : ordered.ThenBy(selector);
        }

        private static IOrderedQueryable<WorkflowTask> OrderTask<TKey>(
            IQueryable<WorkflowTask> source,
            IOrderedQueryable<WorkflowTask>? ordered,
            Expression<Func<WorkflowTask, TKey>> selector,
            bool descending)
        {
            if (ordered is null)
            {
                return descending
                    ? source.OrderByDescending(selector)
                    : source.OrderBy(selector);
            }

            return descending
                ? ordered.ThenByDescending(selector)
                : ordered.ThenBy(selector);
        }

        private static async Task<IReadOnlyDictionary<string, decimal>>
            CalculateTotalsAsync(
                IQueryable<RegistryEntry> entries,
                IReadOnlyList<ReportTotalDefinition> definitions,
                int total,
                CancellationToken cancellationToken)
        {
            var totals = new Dictionary<string, decimal>(
                StringComparer.Ordinal);

            foreach (var definition in definitions)
            {
                var key = $"{definition.Agg}:{definition.Field}";

                if (definition.Agg == "count")
                {
                    totals[key] = total;
                    continue;
                }

                if (total == 0)
                {
                    totals[key] = 0;
                    continue;
                }

                totals[key] = (definition.Agg, definition.Field) switch
                {
                    ("sum", "number") => await entries.SumAsync(
                        entry => (decimal)entry.number,
                        cancellationToken),

                    ("avg", "number") => await entries.AverageAsync(
                        entry => (decimal)entry.number,
                        cancellationToken),

                    ("sum", "year") => await entries.SumAsync(
                        entry => (decimal)entry.year,
                        cancellationToken),

                    ("avg", "year") => await entries.AverageAsync(
                        entry => (decimal)entry.year,
                        cancellationToken),

                    _ => 0
                };
            }

            return totals;
        }

        private static IReadOnlyDictionary<string, object?> BuildRow(
            RegistryEntrySourceRow row,
            IReadOnlyList<ReportColumnDefinition> columns)
        {
            var values = new Dictionary<string, object?>(
                StringComparer.Ordinal)
            {
                ["number"] = row.Number,
                ["year"] = row.Year,
                ["display_number"] = $"{row.Number}/{row.Year}",
                ["registry_type_code"] = row.RegistryTypeCode,
                ["registry_type_name"] = row.RegistryTypeName,
                ["direction"] = row.Direction.ToString(),
                ["registered_at"] = DateOnly.FromDateTime(
                    row.RegisteredAt.UtcDateTime),
                ["deadline"] = row.Deadline,
                ["applicant_name"] = row.ApplicantName,
                ["subject"] = row.Subject,
                ["department_code"] = row.DepartmentCode,
                ["department_name"] = row.DepartmentName,
                ["status"] = row.Status.ToString()
            };

            return columns.ToDictionary(
                column => column.Field,
                column => values[column.Field],
                StringComparer.Ordinal);
        }

        private static IReadOnlyDictionary<string, object?> BuildTaskRow(
            TaskSourceRow row,
            IReadOnlyList<ReportColumnDefinition> columns)
        {
            var values = new Dictionary<string, object?>(
                StringComparer.Ordinal)
            {
                ["title"] = row.Title,
                ["status"] = row.Status.ToString(),
                ["due_date"] = row.DueDate,
                ["completed_at"] = row.CompletedAt.HasValue
                    ? DateOnly.FromDateTime(
                        row.CompletedAt.Value.UtcDateTime)
                    : null,
                ["assignee_email"] = row.AssigneeEmail,
                ["department_code"] = row.DepartmentCode,
                ["entry_display_number"] =
                    $"{row.EntryNumber}/{row.EntryYear}",
                ["entry_subject"] = row.EntrySubject
            };

            return columns.ToDictionary(
                column => column.Field,
                column => values[column.Field],
                StringComparer.Ordinal);
        }

        private static string DefaultAlign(string type)
        {
            return type is "number" or "date" or "code"
                ? "center"
                : "left";
        }

        private static DateTimeOffset ToUtcStart(DateOnly value)
        {
            var dateTime = DateTime.SpecifyKind(
                value.ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Utc);

            return new DateTimeOffset(dateTime);
        }

        private sealed record ParsedReportQuery(
            int Page,
            int PageSize,
            IReadOnlyDictionary<string, object?> Values);

        private sealed record RegistryEntrySourceRow(
            long Number,
            int Year,
            string RegistryTypeCode,
            string RegistryTypeName,
            EntryDirection Direction,
            DateTimeOffset RegisteredAt,
            DateOnly Deadline,
            string ApplicantName,
            string Subject,
            string? DepartmentCode,
            string? DepartmentName,
            EntryStatus Status);

        private sealed record TaskSourceRow(
            string Title,
            WorkflowTaskStatus Status,
            DateOnly? DueDate,
            DateTimeOffset? CompletedAt,
            string? AssigneeEmail,
            string? DepartmentCode,
            long EntryNumber,
            int EntryYear,
            string EntrySubject);

        private async Task<ReportPreviewResponse> PreviewRegistryEntriesAsync(
            ReportDefinitionDocument definition,
            ParsedReportQuery parameters,
            CancellationToken cancellationToken,
            int? maximumRows)
        {
            var entries = ApplyFilters(
                db.RegistryEntries.AsNoTracking(),
                parameters.Values);

            var total = await entries.CountAsync(cancellationToken);

            EnsureExportResultSize(total, maximumRows);

            var totals = await CalculateTotalsAsync(
                entries,
                definition.Totals,
                total,
                cancellationToken);

            var orderedEntries = ApplyOrdering(
                entries,
                definition);

            var sourceRows = await orderedEntries
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .Select(entry => new RegistryEntrySourceRow(
                    entry.number,
                    entry.year,
                    entry.registry_type.code,
                    entry.registry_type.name,
                    entry.direction,
                    entry.registered_at,
                    entry.deadline,
                    entry.applicant_name,
                    entry.subject,
                    entry.department == null
                        ? null
                        : entry.department.code,
                    entry.department == null
                        ? null
                        : entry.department.name,
                    entry.status))
                .ToListAsync(cancellationToken);

            var columns = definition.Columns
                .Select(column => new ReportPreviewColumnResponse(
                    column.Field,
                    column.Label,
                    column.Type,
                    column.Align ?? DefaultAlign(column.Type),
                    column.WidthPct,
                    column.Format))
                .ToList();

            var rows = sourceRows
                .Select(row => BuildRow(
                    row,
                    definition.Columns))
                .ToList();

            return new ReportPreviewResponse(
                columns,
                rows,
                totals,
                new ReportPreviewMetaResponse(
                    parameters.Page,
                    parameters.PageSize,
                    total));
        }

        private async Task<ReportPreviewResponse> PreviewTasksAsync(
            ReportDefinitionDocument definition,
            ParsedReportQuery parameters,
            CancellationToken cancellationToken,
            int? maximumRows)
        {
            var tasks = ApplyTaskFilters(
                db.Tasks.AsNoTracking(),
                parameters.Values);

            var total = await tasks.CountAsync(cancellationToken);

            EnsureExportResultSize(total, maximumRows);

            var orderedTasks = ApplyTaskOrdering(
                tasks,
                definition);

            var sourceRows = await orderedTasks
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .Select(task => new TaskSourceRow(
                    task.title,
                    task.status,
                    task.due_date,
                    task.completed_at,
                    task.assignee_user == null
                        ? null
                        : task.assignee_user.email,
                    task.department == null
                        ? null
                        : task.department.code,
                    task.entry.number,
                    task.entry.year,
                    task.entry.subject))
                .ToListAsync(cancellationToken);

            var columns = definition.Columns
                .Select(column => new ReportPreviewColumnResponse(
                    column.Field,
                    column.Label,
                    column.Type,
                    column.Align ?? DefaultAlign(column.Type),
                    column.WidthPct,
                    column.Format))
                .ToList();

            var rows = sourceRows
                .Select(row => BuildTaskRow(
                    row,
                    definition.Columns))
                .ToList();

            var totals = definition.Totals.ToDictionary(
                item => $"{item.Agg}:{item.Field}",
                _ => (decimal)total,
                StringComparer.Ordinal);

            return new ReportPreviewResponse(
                columns,
                rows,
                totals,
                new ReportPreviewMetaResponse(
                    parameters.Page,
                    parameters.PageSize,
                    total));
        }

        private static void EnsureExportResultSize(
            int total,
            int? maximumRows)
        {
            if (maximumRows is null || total <= maximumRows)
            {
                return;
            }

            throw new ReportPreviewValidationException(
                new Dictionary<string, string[]>
                {
                    ["export"] =
                    [
                        $"Exportul poate conține cel mult {maximumRows:N0} rânduri. Restrânge parametrii raportului."
                    ]
                });
        }
    }

}
