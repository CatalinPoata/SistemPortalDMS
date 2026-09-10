using Shared.Reporting;

namespace API_DMS.Reports
{
    public static class DmsReportDatasets
    {
        public static readonly IReadOnlyList<
            ReportDatasetDescriptor> All =
        [
            new(
            "registry_entries",
            "Poziții de registratură",
            [
                new("number", "Număr", "number", true),
                new("year", "An", "number", true),
                new("display_number", "Număr afișat", "code", false),
                new("registry_type_code", "Cod registru", "code", false),
                new("registry_type_name", "Registru", "text", false),
                new("direction", "Direcție", "code", false),
                new("registered_at", "Data înregistrării", "date", false),
                new("deadline", "Termen", "date", false),
                new("applicant_name", "Solicitant", "text", false),
                new("subject", "Obiectul lucrării", "text", false),
                new("department_code", "Cod compartiment", "code", false),
                new("department_name", "Compartiment", "text", false),
                new("status", "Stare", "code", false)
            ],
            [
                new(
                    "registryTypeId",
                    "Registru",
                    "lookup",
                    "registry_types"),

                new(
                    "departmentId",
                    "Compartiment",
                    "lookup",
                    "departments"),

                new(
                    "status",
                    "Stare",
                    "lookup",
                    "statuses"),

                new(
                    "dateFrom",
                    "De la data",
                    "date",
                    null),

                new(
                    "dateTo",
                    "Până la data",
                    "date",
                    null),

                new(
                    "year",
                    "An",
                    "int",
                    null),

                new(
                    "entryId",
                    "ID poziție",
                    "string",
                    null)
            ]),

        new(
            "tasks",
            "Repartizări și sarcini",
            [
                new("title", "Titlu", "text", false),
                new("status", "Stare", "code", false),
                new("due_date", "Termen", "date", false),
                new("completed_at", "Finalizat la", "date", false),
                new("assignee_email", "Responsabil", "text", false),
                new("department_code", "Cod compartiment", "code", false),
                new("entry_display_number", "Număr poziție", "code", false),
                new("entry_subject", "Obiect poziție", "text", false)
            ],
            [
                new(
                    "departmentId",
                    "Compartiment",
                    "lookup",
                    "departments"),

                new(
                    "status",
                    "Stare",
                    "lookup",
                    "statuses"),

                new(
                    "dateFrom",
                    "De la data",
                    "date",
                    null),

                new(
                    "dateTo",
                    "Până la data",
                    "date",
                    null)
            ])
        ];

        public static ReportDatasetDescriptor? Find(string key)
        {
            return All.SingleOrDefault(dataset =>
                string.Equals(
                    dataset.Key,
                    key,
                    StringComparison.Ordinal));
        }
    }
}
