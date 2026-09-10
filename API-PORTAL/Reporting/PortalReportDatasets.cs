using Shared.Reporting;

namespace API_PORTAL.Reporting;

public static class PortalReportDatasets
{
    public static readonly IReadOnlyList<ReportDatasetDescriptor> All =
    [
        new(
            "submissions",
            "Cereri depuse",
            [
                new("external_id", "ID extern", "code", false),
                new("service_code", "Cod serviciu", "code", false),
                new("service_title", "Serviciu", "text", false),
                new("schema_version", "Versiune formular", "number", true),
                new("applicant_name", "Solicitant", "text", false),
                new("applicant_email", "E-mail solicitant", "text", false),
                new("submitted_at", "Depus la", "date", false),
                new("registered_at", "Înregistrat la", "date", false),
                new("registry_display_number", "Număr registratură", "code", false),
                new("status", "Stare", "code", false),
                new("status_details", "Detalii stare", "text", false)
            ],
            [
                new("serviceId", "Serviciu", "lookup", "service_definitions"),
                new("status", "Stare", "lookup", "statuses"),
                new("dateFrom", "De la data", "date", null),
                new("dateTo", "Până la data", "date", null)
            ]),
        new(
            "appointments",
            "Programări",
            [
                new("reference_code", "Cod referință", "code", false),
                new("appointment_type_code", "Cod tip", "code", false),
                new("appointment_type_name", "Tip programare", "text", false),
                new("starts_at", "Începe la", "date", false),
                new("ends_at", "Se termină la", "date", false),
                new("slot_capacity", "Capacitate", "number", true),
                new("slot_booked_count", "Rezervări", "number", true),
                new("applicant_name", "Solicitant", "text", false),
                new("applicant_email", "E-mail solicitant", "text", false),
                new("status", "Stare", "code", false),
                new("decision_note", "Decizie", "text", false)
            ],
            [
                new("appointmentTypeId", "Tip programare", "lookup", "appointment_types"),
                new("status", "Stare", "lookup", "statuses"),
                new("dateFrom", "De la data", "date", null),
                new("dateTo", "Până la data", "date", null)
            ])
    ];

    public static ReportDatasetDescriptor? Find(string key) => All.SingleOrDefault(
        dataset => string.Equals(dataset.Key, key, StringComparison.Ordinal));
}
