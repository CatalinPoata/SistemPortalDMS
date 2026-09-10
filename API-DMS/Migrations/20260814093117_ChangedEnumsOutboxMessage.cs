using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;


namespace API_DMS.Migrations
{
    public partial class ChangedEnumsOutboxMessage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dms");

            migrationBuilder.CreateTable(
                name: "document_kind",
                schema: "dms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_kind", x => x.id);
                    table.CheckConstraint("CK_document_kind_code_length", "length(code) <= 30");
                    table.CheckConstraint("CK_document_kind_name_length", "length(name) <= 150");
                });

            migrationBuilder.CreateTable(
                name: "inbound_request",
                schema: "dms",
                columns: table => new
                {
                    endpoint = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    response_body = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_request", x => new { x.endpoint, x.idempotency_key });
                    table.CheckConstraint("CK_inbound_request_endpoint_length", "length(endpoint) <= 100");
                    table.CheckConstraint("CK_inbound_request_hash_length", "length(request_hash) = 64");
                    table.CheckConstraint("CK_inbound_request_idempotency_key_length", "length(idempotency_key) <= 100");
                    table.CheckConstraint("CK_inbound_request_response_status", "response_status >= 100 AND response_status <= 599");
                });

            migrationBuilder.CreateTable(
                name: "inbox_event",
                schema: "dms",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_event", x => x.event_id);
                    table.CheckConstraint("CK_inbox_event_event_type_length", "length(event_type) <= 80");
                    table.CheckConstraint("CK_inbox_event_source_length", "length(source) <= 40");
                });

            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "dms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "varchar", nullable: false, defaultValueSql: "'Pending'"),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_message", x => x.id);
                    table.CheckConstraint("CK_outbox_message_aggregate_type_length", "length(aggregate_type) <= 60");
                    table.CheckConstraint("CK_outbox_message_attempts_nonnegative", "attempts >= 0");
                    table.CheckConstraint("CK_outbox_message_event_type_length", "length(event_type) <= 80");
                    table.CheckConstraint("CK_outbox_message_last_error_length", "last_error IS NULL OR length(last_error) <= 2000");
                    table.CheckConstraint("CK_outbox_message_status", "status IN ('Pending', 'Delivered', 'Failed')");
                });

            migrationBuilder.CreateTable(
                name: "registry_type",
                schema: "dms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    direction = table.Column<string>(type: "varchar", nullable: false),
                    start_number = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    default_deadline_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registry_type", x => x.id);
                    table.CheckConstraint("CK_registry_type_code_length", "length(code) <= 30");
                    table.CheckConstraint("CK_registry_type_direction", "direction IN ('In', 'Out', 'Both')");
                    table.CheckConstraint("CK_registry_type_name_length", "length(name) <= 200");
                });

            migrationBuilder.CreateTable(
                name: "user",
                schema: "dms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "varchar", nullable: false, defaultValueSql: "'Citizen'"),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    national_id = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user", x => x.id);
                    table.CheckConstraint("CK_user_address_length", "address IS NULL OR length(address) <= 500");
                    table.CheckConstraint("CK_user_email_length", "length(email) <= 256");
                    table.CheckConstraint("CK_user_full_name_length", "length(full_name) <= 200");
                    table.CheckConstraint("CK_user_national_id_length", "national_id IS NULL OR length(national_id) <= 13");
                    table.CheckConstraint("CK_user_password_hash_length", "length(password_hash) <= 512");
                    table.CheckConstraint("CK_user_phone_length", "phone IS NULL OR length(phone) <= 30");
                    table.CheckConstraint("CK_user_role", "role IN ('Citizen', 'Clerk', 'Admin')");
                });

            migrationBuilder.CreateTable(
                name: "registry_number_counter",
                schema: "dms",
                columns: table => new
                {
                    registry_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registry_number_counter", x => new { x.registry_type_id, x.year });
                    table.ForeignKey(
                        name: "FK_registry_number_counter_registry_type_registry_type_id",
                        column: x => x.registry_type_id,
                        principalSchema: "dms",
                        principalTable: "registry_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "department",
                schema: "dms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    manager_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department", x => x.id);
                    table.CheckConstraint("CK_department_code_length", "length(code) <= 20");
                    table.CheckConstraint("CK_department_name_length", "length(name) <= 200");
                    table.ForeignKey(
                        name: "FK_department_user_manager_user_id",
                        column: x => x.manager_user_id,
                        principalSchema: "dms",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "refresh_token",
                schema: "dms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    replaced_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_token", x => x.id);
                    table.CheckConstraint("CK_refresh_token_token_hash_length", "length(token_hash) <= 128");
                    table.ForeignKey(
                        name: "FK_refresh_token_refresh_token_replaced_by_id",
                        column: x => x.replaced_by_id,
                        principalSchema: "dms",
                        principalTable: "refresh_token",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_token_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "dms",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_definition",
                schema: "dms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    dataset_key = table.Column<string>(type: "text", nullable: false),
                    definition = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_definition", x => x.id);
                    table.CheckConstraint("CK_report_definition_code_length", "length(code) <= 50");
                    table.CheckConstraint("CK_report_definition_dataset_key_length", "length(dataset_key) <= 50");
                    table.CheckConstraint("CK_report_definition_name_length", "length(name) <= 200");
                    table.CheckConstraint("CK_report_definition_version_positive", "version >= 1");
                    table.ForeignKey(
                        name: "FK_report_definition_user_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalSchema: "dms",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registry_entry",
                schema: "dms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registry_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<long>(type: "bigint", nullable: false),
                    direction = table.Column<string>(type: "varchar", nullable: false, defaultValueSql: "'In'"),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    subject = table.Column<string>(type: "text", nullable: false),
                    applicant_name = table.Column<string>(type: "text", nullable: false),
                    applicant_national_id = table.Column<string>(type: "text", nullable: true),
                    applicant_email = table.Column<string>(type: "text", nullable: true),
                    applicant_phone = table.Column<string>(type: "text", nullable: true),
                    applicant_address = table.Column<string>(type: "text", nullable: true),
                    source_doc_number = table.Column<string>(type: "text", nullable: true),
                    source_doc_date = table.Column<DateOnly>(type: "date", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_code = table.Column<string>(type: "text", nullable: true),
                    form_values = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    deadline = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "varchar", nullable: false, defaultValueSql: "'Registered'"),
                    status_note = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registry_entry", x => x.id);
                    table.CheckConstraint("CK_registry_entry_applicant_address_length", "applicant_address IS NULL OR length(applicant_address) <= 500");
                    table.CheckConstraint("CK_registry_entry_applicant_email_length", "applicant_email IS NULL OR length(applicant_email) <= 256");
                    table.CheckConstraint("CK_registry_entry_applicant_name_length", "length(applicant_name) <= 200");
                    table.CheckConstraint("CK_registry_entry_applicant_national_id_length", "applicant_national_id IS NULL OR length(applicant_national_id) <= 13");
                    table.CheckConstraint("CK_registry_entry_applicant_phone_length", "applicant_phone IS NULL OR length(applicant_phone) <= 30");
                    table.CheckConstraint("CK_registry_entry_direction", "direction IN ('In', 'Out')");
                    table.CheckConstraint("CK_registry_entry_service_code_length", "service_code IS NULL OR length(service_code) <= 50");
                    table.CheckConstraint("CK_registry_entry_source_doc_number_length", "source_doc_number IS NULL OR length(source_doc_number) <= 60");
                    table.CheckConstraint("CK_registry_entry_status", "status IN ('Submitted', 'Registered', 'InReview', 'InfoRequested', 'Completed', 'Rejected', 'Cancelled')");
                    table.CheckConstraint("CK_registry_entry_status_note_length", "status_note IS NULL OR length(status_note) <= 1000");
                    table.CheckConstraint("CK_registry_entry_subject_length", "length(subject) <= 1000");
                    table.ForeignKey(
                        name: "FK_registry_entry_department_department_id",
                        column: x => x.department_id,
                        principalSchema: "dms",
                        principalTable: "department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_registry_entry_registry_type_registry_type_id",
                        column: x => x.registry_type_id,
                        principalSchema: "dms",
                        principalTable: "registry_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registry_entry_user_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "dms",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entry_event",
                schema: "dms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "varchar", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entry_event", x => x.id);
                    table.CheckConstraint("CK_entry_event_message_length", "length(message) <= 1000");
                    table.CheckConstraint("CK_entry_event_type", "type IN ('Created', 'StatusChanged', 'Assigned', 'DocumentAdded', 'TaskCompleted')");
                    table.ForeignKey(
                        name: "FK_entry_event_registry_entry_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "dms",
                        principalTable: "registry_entry",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_entry_event_user_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "dms",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "registry_document",
                schema: "dms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    direction = table.Column<string>(type: "varchar", nullable: false),
                    document_kind_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_date = table.Column<DateOnly>(type: "date", nullable: true),
                    issuer = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    original_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "text", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registry_document", x => x.id);
                    table.CheckConstraint("CK_registry_document_content_type_length", "length(content_type) <= 120");
                    table.CheckConstraint("CK_registry_document_direction", "direction IN ('In', 'Out')");
                    table.CheckConstraint("CK_registry_document_issuer_length", "issuer IS NULL OR length(issuer) <= 200");
                    table.CheckConstraint("CK_registry_document_note_length", "note IS NULL OR length(note) <= 500");
                    table.CheckConstraint("CK_registry_document_original_name_length", "length(original_name) <= 255");
                    table.CheckConstraint("CK_registry_document_sha256_length", "length(sha256) <= 64");
                    table.CheckConstraint("CK_registry_document_storage_key_length", "length(storage_key) <= 200");
                    table.ForeignKey(
                        name: "FK_registry_document_document_kind_document_kind_id",
                        column: x => x.document_kind_id,
                        principalSchema: "dms",
                        principalTable: "document_kind",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registry_document_registry_entry_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "dms",
                        principalTable: "registry_entry",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_registry_document_user_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalSchema: "dms",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task",
                schema: "dms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    instructions = table.Column<string>(type: "text", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "varchar", nullable: false, defaultValueSql: "'Open'"),
                    resolution_note = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task", x => x.id);
                    table.CheckConstraint("CK_task_assignee_or_department", "assignee_user_id IS NOT NULL OR department_id IS NOT NULL");
                    table.CheckConstraint("CK_task_resolution_note_length", "resolution_note IS NULL OR length(resolution_note) <= 1000");
                    table.CheckConstraint("CK_task_status", "status IN ('Open', 'Done', 'Cancelled')");
                    table.CheckConstraint("CK_task_title_length", "length(title) <= 200");
                    table.ForeignKey(
                        name: "FK_task_department_department_id",
                        column: x => x.department_id,
                        principalSchema: "dms",
                        principalTable: "department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_task_registry_entry_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "dms",
                        principalTable: "registry_entry",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_user_assignee_user_id",
                        column: x => x.assignee_user_id,
                        principalSchema: "dms",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_task_user_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "dms",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_department_code",
                schema: "dms",
                table: "department",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_department_manager_user_id",
                schema: "dms",
                table: "department",
                column: "manager_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_kind_code",
                schema: "dms",
                table: "document_kind",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entry_event_actor_user_id",
                schema: "dms",
                table: "entry_event",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_entry_event_entry_id",
                schema: "dms",
                table: "entry_event",
                column: "entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_status_next_attempt_at",
                schema: "dms",
                table: "outbox_message",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_token_replaced_by_id",
                schema: "dms",
                table: "refresh_token",
                column: "replaced_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_token_user_id",
                schema: "dms",
                table: "refresh_token",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_registry_document_document_kind_id",
                schema: "dms",
                table: "registry_document",
                column: "document_kind_id");

            migrationBuilder.CreateIndex(
                name: "IX_registry_document_entry_id",
                schema: "dms",
                table: "registry_document",
                column: "entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_registry_document_external_file_id",
                schema: "dms",
                table: "registry_document",
                column: "external_file_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registry_document_uploaded_by_user_id",
                schema: "dms",
                table: "registry_document",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_registry_entry_applicant_name",
                schema: "dms",
                table: "registry_entry",
                column: "applicant_name");

            migrationBuilder.CreateIndex(
                name: "IX_registry_entry_created_by_user_id",
                schema: "dms",
                table: "registry_entry",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_registry_entry_department_id",
                schema: "dms",
                table: "registry_entry",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_registry_entry_external_id",
                schema: "dms",
                table: "registry_entry",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registry_entry_registry_type_id_year_number",
                schema: "dms",
                table: "registry_entry",
                columns: new[] { "registry_type_id", "year", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registry_entry_registry_type_id_year_registered_at",
                schema: "dms",
                table: "registry_entry",
                columns: new[] { "registry_type_id", "year", "registered_at" });

            migrationBuilder.CreateIndex(
                name: "IX_registry_entry_status",
                schema: "dms",
                table: "registry_entry",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_registry_entry_subject",
                schema: "dms",
                table: "registry_entry",
                column: "subject");

            migrationBuilder.CreateIndex(
                name: "IX_registry_type_code",
                schema: "dms",
                table: "registry_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_report_definition_code",
                schema: "dms",
                table: "report_definition",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_report_definition_updated_by_user_id",
                schema: "dms",
                table: "report_definition",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_assignee_user_id",
                schema: "dms",
                table: "task",
                column: "assignee_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_created_by_user_id",
                schema: "dms",
                table: "task",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_department_id",
                schema: "dms",
                table: "task",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_entry_id",
                schema: "dms",
                table: "task",
                column: "entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_email",
                schema: "dms",
                table: "user",
                column: "email",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entry_event",
                schema: "dms");

            migrationBuilder.DropTable(
                name: "inbound_request",
                schema: "dms");

            migrationBuilder.DropTable(
                name: "inbox_event",
                schema: "dms");

            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "dms");

            migrationBuilder.DropTable(
                name: "refresh_token",
                schema: "dms");

            migrationBuilder.DropTable(
                name: "registry_document",
                schema: "dms");

            migrationBuilder.DropTable(
                name: "registry_number_counter",
                schema: "dms");

            migrationBuilder.DropTable(
                name: "report_definition",
                schema: "dms");

            migrationBuilder.DropTable(
                name: "task",
                schema: "dms");

            migrationBuilder.DropTable(
                name: "document_kind",
                schema: "dms");

            migrationBuilder.DropTable(
                name: "registry_entry",
                schema: "dms");

            migrationBuilder.DropTable(
                name: "department",
                schema: "dms");

            migrationBuilder.DropTable(
                name: "registry_type",
                schema: "dms");

            migrationBuilder.DropTable(
                name: "user",
                schema: "dms");
        }
    }
}
