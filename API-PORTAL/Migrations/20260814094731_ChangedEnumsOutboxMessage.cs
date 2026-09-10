using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;


namespace API_PORTAL.Migrations
{
    public partial class ChangedEnumsOutboxMessage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "portal");

            migrationBuilder.CreateTable(
                name: "appointment_type",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "text", nullable: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    requires_confirmation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    max_days_ahead = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_type", x => x.id);
                    table.CheckConstraint("CK_appointment_type_code_length", "length(code) <= 40");
                    table.CheckConstraint("CK_appointment_type_description_length", "description IS NULL OR length(description) <= 1000");
                    table.CheckConstraint("CK_appointment_type_duration_minutes_positive", "duration_minutes > 0");
                    table.CheckConstraint("CK_appointment_type_location_length", "location IS NULL OR length(location) <= 250");
                    table.CheckConstraint("CK_appointment_type_max_days_ahead_nonnegative", "max_days_ahead >= 0");
                    table.CheckConstraint("CK_appointment_type_name_length", "length(name) <= 200");
                });

            migrationBuilder.CreateTable(
                name: "article",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_article", x => x.id);
                    table.CheckConstraint("CK_article_slug_format", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.CheckConstraint("CK_article_slug_length", "length(slug) <= 160");
                    table.CheckConstraint("CK_article_summary_length", "summary IS NULL OR length(summary) <= 500");
                    table.CheckConstraint("CK_article_title_length", "length(title) <= 250");
                });

            migrationBuilder.CreateTable(
                name: "inbound_request",
                schema: "portal",
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
                schema: "portal",
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
                schema: "portal",
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
                name: "public_registry",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_registry", x => x.id);
                    table.CheckConstraint("CK_public_registry_code_length", "length(code) <= 40");
                    table.CheckConstraint("CK_public_registry_description_length", "description IS NULL OR length(description) <= 1000");
                    table.CheckConstraint("CK_public_registry_display_order_non_negative", "display_order >= 0");
                    table.CheckConstraint("CK_public_registry_name_length", "length(name) <= 200");
                });

            migrationBuilder.CreateTable(
                name: "service_definition",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    short_description = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    registry_type_code = table.Column<string>(type: "text", nullable: false),
                    form_schema = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    requires_attachment = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    max_attachments = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_definition", x => x.id);
                    table.CheckConstraint("CK_service_definition_code_format", "code ~ '^[a-z0-9-]+$'");
                    table.CheckConstraint("CK_service_definition_code_length", "length(code) <= 50");
                    table.CheckConstraint("CK_service_definition_max_attachments_non_negative", "max_attachments >= 0");
                    table.CheckConstraint("CK_service_definition_registry_type_code_length", "length(registry_type_code) <= 30");
                    table.CheckConstraint("CK_service_definition_schema_version_positive", "schema_version >= 1");
                    table.CheckConstraint("CK_service_definition_short_description_length", "short_description IS NULL OR length(short_description) <= 500");
                    table.CheckConstraint("CK_service_definition_title_length", "length(title) <= 200");
                });

            migrationBuilder.CreateTable(
                name: "survey",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    allow_anonymous = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    show_results = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey", x => x.id);
                    table.CheckConstraint("CK_survey_code_length", "length(code) <= 50");
                    table.CheckConstraint("CK_survey_dates", "ends_at IS NULL OR starts_at IS NULL OR ends_at > starts_at");
                    table.CheckConstraint("CK_survey_title_length", "length(title) <= 250");
                });

            migrationBuilder.CreateTable(
                name: "user",
                schema: "portal",
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
                name: "appointment_slot",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    booked_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_slot", x => x.id);
                    table.CheckConstraint("CK_appointment_slot_booked_count_valid", "booked_count >= 0 AND booked_count <= capacity");
                    table.CheckConstraint("CK_appointment_slot_capacity_positive", "capacity > 0");
                    table.CheckConstraint("CK_appointment_slot_ends_after_starts", "ends_at > starts_at");
                    table.ForeignKey(
                        name: "FK_appointment_slot_appointment_type_appointment_type_id",
                        column: x => x.appointment_type_id,
                        principalSchema: "portal",
                        principalTable: "appointment_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "public_registry_entry",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    registry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_number = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    entry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_registry_entry", x => x.id);
                    table.CheckConstraint("CK_public_registry_entry_description_length", "description IS NULL OR length(description) <= 2000");
                    table.CheckConstraint("CK_public_registry_entry_position_number_length", "length(position_number) <= 40");
                    table.CheckConstraint("CK_public_registry_entry_title_length", "length(title) <= 500");
                    table.ForeignKey(
                        name: "FK_public_registry_entry_public_registry_registry_id",
                        column: x => x.registry_id,
                        principalSchema: "portal",
                        principalTable: "public_registry",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_question",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "varchar", nullable: false),
                    options = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_question", x => x.id);
                    table.CheckConstraint("CK_survey_question_display_order_non_negative", "display_order >= 0");
                    table.CheckConstraint("CK_survey_question_key_length", "length(key) <= 60");
                    table.CheckConstraint("CK_survey_question_text_length", "length(text) <= 1000");
                    table.CheckConstraint("CK_survey_question_type", "type IN ('SingleChoice', 'MultiChoice', 'Rating', 'FreeText')");
                    table.ForeignKey(
                        name: "FK_survey_question_survey_survey_id",
                        column: x => x.survey_id,
                        principalSchema: "portal",
                        principalTable: "survey",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    link_url = table.Column<string>(type: "text", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification", x => x.id);
                    table.CheckConstraint("CK_notification_link_url_length", "length(link_url) <= 500");
                    table.CheckConstraint("CK_notification_subject_length", "length(subject) <= 200");
                    table.ForeignKey(
                        name: "FK_notification_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "portal",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_token",
                schema: "portal",
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
                        principalSchema: "portal",
                        principalTable: "refresh_token",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_token_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "portal",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_definition",
                schema: "portal",
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
                        principalSchema: "portal",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "submission",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    form_snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    values = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "varchar", nullable: false, defaultValueSql: "'Submitted'"),
                    status_details = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    registry_number = table.Column<long>(type: "bigint", nullable: true),
                    registry_year = table.Column<int>(type: "integer", nullable: true),
                    registry_display_number = table.Column<string>(type: "text", nullable: true),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    dms_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission", x => x.id);
                    table.CheckConstraint("CK_submission_registry_display_number_length", "registry_display_number IS NULL OR length(registry_display_number) <= 30");
                    table.CheckConstraint("CK_submission_status", "status IN ('Submitted', 'Registered', 'InReview', 'InfoRequested', 'Completed', 'Rejected', 'Cancelled')");
                    table.CheckConstraint("CK_submission_status_details_length", "status_details IS NULL OR length(status_details) <= 1000");
                    table.ForeignKey(
                        name: "FK_submission_service_definition_service_id",
                        column: x => x.service_id,
                        principalSchema: "portal",
                        principalTable: "service_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_submission_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "portal",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_response",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    answers = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_response", x => x.id);
                    table.ForeignKey(
                        name: "FK_survey_response_survey_survey_id",
                        column: x => x.survey_id,
                        principalSchema: "portal",
                        principalTable: "survey",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_survey_response_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "portal",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "appointment",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "varchar", nullable: false, defaultValueSql: "'Requested'"),
                    notes = table.Column<string>(type: "text", nullable: true),
                    decision_note = table.Column<string>(type: "text", nullable: true),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    reference_code = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment", x => x.id);
                    table.CheckConstraint("CK_appointment_decision_note_length", "decision_note IS NULL OR length(decision_note) <= 1000");
                    table.CheckConstraint("CK_appointment_notes_length", "notes IS NULL OR length(notes) <= 1000");
                    table.CheckConstraint("CK_appointment_reference_code_length", "length(reference_code) <= 20");
                    table.CheckConstraint("CK_appointment_status", "status IN ('Requested', 'Confirmed', 'Rejected', 'Cancelled', 'Completed', 'NoShow')");
                    table.ForeignKey(
                        name: "FK_appointment_appointment_slot_slot_id",
                        column: x => x.slot_id,
                        principalSchema: "portal",
                        principalTable: "appointment_slot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_appointment_user_decided_by_user_id",
                        column: x => x.decided_by_user_id,
                        principalSchema: "portal",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appointment_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "portal",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "public_registry_document",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    original_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "text", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_registry_document", x => x.id);
                    table.CheckConstraint("CK_public_registry_document_content_type_length", "length(content_type) <= 120");
                    table.CheckConstraint("CK_public_registry_document_display_order_non_negative", "display_order >= 0");
                    table.CheckConstraint("CK_public_registry_document_original_name_length", "length(original_name) <= 255");
                    table.CheckConstraint("CK_public_registry_document_sha256_length", "length(sha256) <= 64");
                    table.CheckConstraint("CK_public_registry_document_size_bytes_non_negative", "size_bytes >= 0");
                    table.CheckConstraint("CK_public_registry_document_storage_key_length", "length(storage_key) <= 200");
                    table.ForeignKey(
                        name: "FK_public_registry_document_public_registry_entry_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "portal",
                        principalTable: "public_registry_entry",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_file",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "text", nullable: true),
                    kind = table.Column<string>(type: "varchar", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    original_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_file", x => x.id);
                    table.CheckConstraint("CK_submission_file_content_type_length", "length(content_type) <= 120");
                    table.CheckConstraint("CK_submission_file_field_key_length", "field_key IS NULL OR length(field_key) <= 60");
                    table.CheckConstraint("CK_submission_file_kind", "kind IN ('Application', 'Attachment', 'Response')");
                    table.CheckConstraint("CK_submission_file_original_name_length", "length(original_name) <= 255");
                    table.CheckConstraint("CK_submission_file_sha256_length", "length(sha256) <= 64");
                    table.CheckConstraint("CK_submission_file_storage_key_length", "length(storage_key) <= 200");
                    table.ForeignKey(
                        name: "FK_submission_file_submission_submission_id",
                        column: x => x.submission_id,
                        principalSchema: "portal",
                        principalTable: "submission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_event",
                schema: "portal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    type = table.Column<string>(type: "varchar", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_event", x => x.id);
                    table.CheckConstraint("CK_submission_event_message_length", "length(message) <= 1000");
                    table.CheckConstraint("CK_submission_event_type", "type IN ('Submitted', 'Registered', 'StatusChanged', 'InfoRequested', 'FileAdded', 'Completed', 'Rejected', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_submission_event_submission_file_file_id",
                        column: x => x.file_id,
                        principalSchema: "portal",
                        principalTable: "submission_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_submission_event_submission_submission_id",
                        column: x => x.submission_id,
                        principalSchema: "portal",
                        principalTable: "submission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_decided_by_user_id",
                schema: "portal",
                table: "appointment",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_appointment_reference_code",
                schema: "portal",
                table: "appointment",
                column: "reference_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_appointment_slot_id_user_id",
                schema: "portal",
                table: "appointment",
                columns: new[] { "slot_id", "user_id" },
                unique: true,
                filter: "status IN ('Requested', 'Confirmed')");

            migrationBuilder.CreateIndex(
                name: "IX_appointment_user_id",
                schema: "portal",
                table: "appointment",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_appointment_slot_appointment_type_id_starts_at",
                schema: "portal",
                table: "appointment_slot",
                columns: new[] { "appointment_type_id", "starts_at" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_appointment_type_code",
                schema: "portal",
                table: "appointment_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_article_slug",
                schema: "portal",
                table: "article",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_user_id",
                schema: "portal",
                table: "notification",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_status_next_attempt_at",
                schema: "portal",
                table: "outbox_message",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_public_registry_code",
                schema: "portal",
                table: "public_registry",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_registry_document_entry_id",
                schema: "portal",
                table: "public_registry_document",
                column: "entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_public_registry_document_entry_id_display_order",
                schema: "portal",
                table: "public_registry_document",
                columns: new[] { "entry_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_public_registry_entry_registry_id_is_published",
                schema: "portal",
                table: "public_registry_entry",
                columns: new[] { "registry_id", "is_published" });

            migrationBuilder.CreateIndex(
                name: "IX_public_registry_entry_registry_id_position_number",
                schema: "portal",
                table: "public_registry_entry",
                columns: new[] { "registry_id", "position_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_token_replaced_by_id",
                schema: "portal",
                table: "refresh_token",
                column: "replaced_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_token_user_id",
                schema: "portal",
                table: "refresh_token",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_report_definition_code",
                schema: "portal",
                table: "report_definition",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_report_definition_updated_by_user_id",
                schema: "portal",
                table: "report_definition",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_definition_code",
                schema: "portal",
                table: "service_definition",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_external_id",
                schema: "portal",
                table: "submission",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_service_id",
                schema: "portal",
                table: "submission",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "IX_submission_user_id",
                schema: "portal",
                table: "submission",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_submission_event_file_id",
                schema: "portal",
                table: "submission_event",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "IX_submission_event_submission_id",
                schema: "portal",
                table: "submission_event",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "IX_submission_file_submission_id",
                schema: "portal",
                table: "submission_file",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_code",
                schema: "portal",
                table: "survey",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_question_survey_id_display_order",
                schema: "portal",
                table: "survey_question",
                columns: new[] { "survey_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_question_survey_id_key",
                schema: "portal",
                table: "survey_question",
                columns: new[] { "survey_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_response_survey_id_user_id",
                schema: "portal",
                table: "survey_response",
                columns: new[] { "survey_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_survey_response_user_id",
                schema: "portal",
                table: "survey_response",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_email",
                schema: "portal",
                table: "user",
                column: "email",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "article",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "inbound_request",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "inbox_event",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "notification",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "public_registry_document",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "refresh_token",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "report_definition",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "submission_event",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "survey_question",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "survey_response",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "appointment_slot",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "public_registry_entry",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "submission_file",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "survey",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "appointment_type",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "public_registry",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "submission",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "service_definition",
                schema: "portal");

            migrationBuilder.DropTable(
                name: "user",
                schema: "portal");
        }
    }
}
