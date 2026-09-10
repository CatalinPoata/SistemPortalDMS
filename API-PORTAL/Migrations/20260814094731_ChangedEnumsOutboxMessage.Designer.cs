using System;
using System.Text.Json;
using API_PORTAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;


namespace API_PORTAL.Migrations
{
    [DbContext(typeof(PortalDbContext))]
    [Migration("20260814094731_ChangedEnumsOutboxMessage")]
    partial class ChangedEnumsOutboxMessage
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasDefaultSchema("portal")
                .HasAnnotation("ProductVersion", "9.0.19")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("API_DMS.Entities.InboundRequest", b =>
                {
                    b.Property<string>("endpoint")
                        .HasColumnType("text");

                    b.Property<string>("idempotency_key")
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("request_hash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<JsonDocument>("response_body")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<int>("response_status")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("endpoint", "idempotency_key");

                    b.ToTable("inbound_request", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_inbound_request_endpoint_length", "length(endpoint) <= 100");

                            t.HasCheckConstraint("CK_inbound_request_hash_length", "length(request_hash) = 64");

                            t.HasCheckConstraint("CK_inbound_request_idempotency_key_length", "length(idempotency_key) <= 100");

                            t.HasCheckConstraint("CK_inbound_request_response_status", "response_status >= 100 AND response_status <= 599");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.Appointment", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<DateTimeOffset?>("decided_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid?>("decided_by_user_id")
                        .HasColumnType("uuid");

                    b.Property<string>("decision_note")
                        .HasColumnType("text");

                    b.Property<string>("notes")
                        .HasColumnType("text");

                    b.Property<string>("reference_code")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("slot_id")
                        .HasColumnType("uuid");

                    b.Property<string>("status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("varchar")
                        .HasDefaultValueSql("'Requested'");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid>("user_id")
                        .HasColumnType("uuid");

                    b.HasKey("id");

                    b.HasIndex("decided_by_user_id");

                    b.HasIndex("reference_code")
                        .IsUnique();

                    b.HasIndex("user_id");

                    b.HasIndex("slot_id", "user_id")
                        .IsUnique()
                        .HasFilter("status IN ('Requested', 'Confirmed')");

                    b.ToTable("appointment", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_appointment_decision_note_length", "decision_note IS NULL OR length(decision_note) <= 1000");

                            t.HasCheckConstraint("CK_appointment_notes_length", "notes IS NULL OR length(notes) <= 1000");

                            t.HasCheckConstraint("CK_appointment_reference_code_length", "length(reference_code) <= 20");

                            t.HasCheckConstraint("CK_appointment_status", "status IN ('Requested', 'Confirmed', 'Rejected', 'Cancelled', 'Completed', 'NoShow')");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.AppointmentSlot", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("appointment_type_id")
                        .HasColumnType("uuid");

                    b.Property<int>("booked_count")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0);

                    b.Property<int>("capacity")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(1);

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<DateTimeOffset>("ends_at")
                        .HasColumnType("timestamptz");

                    b.Property<bool>("is_blocked")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<DateTimeOffset>("starts_at")
                        .HasColumnType("timestamptz");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("appointment_type_id", "starts_at")
                        .IsUnique();

                    b.ToTable("appointment_slot", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_appointment_slot_booked_count_valid", "booked_count >= 0 AND booked_count <= capacity");

                            t.HasCheckConstraint("CK_appointment_slot_capacity_positive", "capacity > 0");

                            t.HasCheckConstraint("CK_appointment_slot_ends_after_starts", "ends_at > starts_at");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.AppointmentType", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("code")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("description")
                        .HasColumnType("text");

                    b.Property<int>("duration_minutes")
                        .HasColumnType("integer");

                    b.Property<bool>("is_active")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(true);

                    b.Property<string>("location")
                        .HasColumnType("text");

                    b.Property<int>("max_days_ahead")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(30);

                    b.Property<string>("name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("requires_confirmation")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(true);

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("code")
                        .IsUnique();

                    b.ToTable("appointment_type", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_appointment_type_code_length", "length(code) <= 40");

                            t.HasCheckConstraint("CK_appointment_type_description_length", "description IS NULL OR length(description) <= 1000");

                            t.HasCheckConstraint("CK_appointment_type_duration_minutes_positive", "duration_minutes > 0");

                            t.HasCheckConstraint("CK_appointment_type_location_length", "location IS NULL OR length(location) <= 250");

                            t.HasCheckConstraint("CK_appointment_type_max_days_ahead_nonnegative", "max_days_ahead >= 0");

                            t.HasCheckConstraint("CK_appointment_type_name_length", "length(name) <= 200");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.Article", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("body")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<bool>("is_published")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<DateTimeOffset?>("published_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("slug")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("summary")
                        .HasColumnType("text");

                    b.Property<string>("title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("slug")
                        .IsUnique();

                    b.ToTable("article", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_article_slug_format", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");

                            t.HasCheckConstraint("CK_article_slug_length", "length(slug) <= 160");

                            t.HasCheckConstraint("CK_article_summary_length", "summary IS NULL OR length(summary) <= 500");

                            t.HasCheckConstraint("CK_article_title_length", "length(title) <= 250");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.InboxEvent", b =>
                {
                    b.Property<Guid>("event_id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("event_type")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("payload")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<DateTimeOffset>("processed_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("source")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("event_id");

                    b.ToTable("inbox_event", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_inbox_event_event_type_length", "length(event_type) <= 80");

                            t.HasCheckConstraint("CK_inbox_event_source_length", "length(source) <= 40");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.Notification", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("body")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("link_url")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset?>("read_at")
                        .HasColumnType("timestamptz");

                    b.Property<DateTimeOffset?>("sent_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("subject")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid>("user_id")
                        .HasColumnType("uuid");

                    b.HasKey("id");

                    b.HasIndex("user_id");

                    b.ToTable("notification", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_notification_link_url_length", "length(link_url) <= 500");

                            t.HasCheckConstraint("CK_notification_subject_length", "length(subject) <= 200");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.OutboxMessage", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("aggregate_id")
                        .HasColumnType("uuid");

                    b.Property<string>("aggregate_type")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("attempts")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0);

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<DateTimeOffset?>("delivered_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("event_type")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("last_error")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("next_attempt_at")
                        .HasColumnType("timestamptz");

                    b.Property<JsonDocument>("payload")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<string>("status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("varchar")
                        .HasDefaultValueSql("'Pending'");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("status", "next_attempt_at");

                    b.ToTable("outbox_message", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_outbox_message_aggregate_type_length", "length(aggregate_type) <= 60");

                            t.HasCheckConstraint("CK_outbox_message_attempts_nonnegative", "attempts >= 0");

                            t.HasCheckConstraint("CK_outbox_message_event_type_length", "length(event_type) <= 80");

                            t.HasCheckConstraint("CK_outbox_message_last_error_length", "last_error IS NULL OR length(last_error) <= 2000");

                            t.HasCheckConstraint("CK_outbox_message_status", "status IN ('Pending', 'Delivered', 'Failed')");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.PublicRegistry", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("code")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("description")
                        .HasColumnType("text");

                    b.Property<int>("display_order")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0);

                    b.Property<bool>("is_published")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<string>("name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("code")
                        .IsUnique();

                    b.ToTable("public_registry", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_public_registry_code_length", "length(code) <= 40");

                            t.HasCheckConstraint("CK_public_registry_description_length", "description IS NULL OR length(description) <= 1000");

                            t.HasCheckConstraint("CK_public_registry_display_order_non_negative", "display_order >= 0");

                            t.HasCheckConstraint("CK_public_registry_name_length", "length(name) <= 200");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.PublicRegistryDocument", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("content_type")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<int>("display_order")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0);

                    b.Property<Guid>("entry_id")
                        .HasColumnType("uuid");

                    b.Property<string>("original_name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("sha256")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("size_bytes")
                        .HasColumnType("bigint");

                    b.Property<string>("storage_key")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("entry_id");

                    b.HasIndex("entry_id", "display_order");

                    b.ToTable("public_registry_document", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_public_registry_document_content_type_length", "length(content_type) <= 120");

                            t.HasCheckConstraint("CK_public_registry_document_display_order_non_negative", "display_order >= 0");

                            t.HasCheckConstraint("CK_public_registry_document_original_name_length", "length(original_name) <= 255");

                            t.HasCheckConstraint("CK_public_registry_document_sha256_length", "length(sha256) <= 64");

                            t.HasCheckConstraint("CK_public_registry_document_size_bytes_non_negative", "size_bytes >= 0");

                            t.HasCheckConstraint("CK_public_registry_document_storage_key_length", "length(storage_key) <= 200");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.PublicRegistryEntry", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("description")
                        .HasColumnType("text");

                    b.Property<DateOnly>("entry_date")
                        .HasColumnType("date");

                    b.Property<bool>("is_published")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<string>("position_number")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("registry_id")
                        .HasColumnType("uuid");

                    b.Property<string>("title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("registry_id", "is_published");

                    b.HasIndex("registry_id", "position_number")
                        .IsUnique();

                    b.ToTable("public_registry_entry", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_public_registry_entry_description_length", "description IS NULL OR length(description) <= 2000");

                            t.HasCheckConstraint("CK_public_registry_entry_position_number_length", "length(position_number) <= 40");

                            t.HasCheckConstraint("CK_public_registry_entry_title_length", "length(title) <= 500");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.RefreshToken", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<DateTimeOffset>("expires_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid?>("replaced_by_id")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset?>("revoked_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("token_hash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid>("user_id")
                        .HasColumnType("uuid");

                    b.HasKey("id");

                    b.HasIndex("replaced_by_id");

                    b.HasIndex("user_id");

                    b.ToTable("refresh_token", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_refresh_token_token_hash_length", "length(token_hash) <= 128");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.ReportDefinition", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("code")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("dataset_key")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<JsonDocument>("definition")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<bool>("is_system")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<string>("name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid>("updated_by_user_id")
                        .HasColumnType("uuid");

                    b.Property<int>("version")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(1);

                    b.HasKey("id");

                    b.HasIndex("code")
                        .IsUnique();

                    b.HasIndex("updated_by_user_id");

                    b.ToTable("report_definition", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_report_definition_code_length", "length(code) <= 50");

                            t.HasCheckConstraint("CK_report_definition_dataset_key_length", "length(dataset_key) <= 50");

                            t.HasCheckConstraint("CK_report_definition_name_length", "length(name) <= 200");

                            t.HasCheckConstraint("CK_report_definition_version_positive", "version >= 1");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.ServiceDefinition", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("code")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("description")
                        .HasColumnType("text");

                    b.Property<int>("display_order")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0);

                    b.Property<JsonDocument>("form_schema")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<bool>("is_published")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<int>("max_attachments")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(3);

                    b.Property<string>("registry_type_code")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("requires_attachment")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<int>("schema_version")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(1);

                    b.Property<string>("short_description")
                        .HasColumnType("text");

                    b.Property<string>("title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("code")
                        .IsUnique();

                    b.ToTable("service_definition", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_service_definition_code_format", "code ~ '^[a-z0-9-]+$'");

                            t.HasCheckConstraint("CK_service_definition_code_length", "length(code) <= 50");

                            t.HasCheckConstraint("CK_service_definition_max_attachments_non_negative", "max_attachments >= 0");

                            t.HasCheckConstraint("CK_service_definition_registry_type_code_length", "length(registry_type_code) <= 30");

                            t.HasCheckConstraint("CK_service_definition_schema_version_positive", "schema_version >= 1");

                            t.HasCheckConstraint("CK_service_definition_short_description_length", "short_description IS NULL OR length(short_description) <= 500");

                            t.HasCheckConstraint("CK_service_definition_title_length", "length(title) <= 200");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.Submission", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid?>("dms_entry_id")
                        .HasColumnType("uuid");

                    b.Property<Guid>("external_id")
                        .HasColumnType("uuid");

                    b.Property<JsonDocument>("form_snapshot")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<DateTimeOffset?>("registered_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("registry_display_number")
                        .HasColumnType("text");

                    b.Property<long?>("registry_number")
                        .HasColumnType("bigint");

                    b.Property<int?>("registry_year")
                        .HasColumnType("integer");

                    b.Property<int>("schema_version")
                        .HasColumnType("integer");

                    b.Property<Guid>("service_id")
                        .HasColumnType("uuid");

                    b.Property<string>("status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("varchar")
                        .HasDefaultValueSql("'Submitted'");

                    b.Property<string>("status_details")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("submitted_at")
                        .HasColumnType("timestamptz");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid>("user_id")
                        .HasColumnType("uuid");

                    b.Property<JsonDocument>("values")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.HasKey("id");

                    b.HasIndex("external_id")
                        .IsUnique();

                    b.HasIndex("service_id");

                    b.HasIndex("user_id");

                    b.ToTable("submission", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_submission_registry_display_number_length", "registry_display_number IS NULL OR length(registry_display_number) <= 30");

                            t.HasCheckConstraint("CK_submission_status", "status IN ('Submitted', 'Registered', 'InReview', 'InfoRequested', 'Completed', 'Rejected', 'Cancelled')");

                            t.HasCheckConstraint("CK_submission_status_details_length", "status_details IS NULL OR length(status_details) <= 1000");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.SubmissionEvent", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid?>("file_id")
                        .HasColumnType("uuid");

                    b.Property<string>("message")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("occurred_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid>("submission_id")
                        .HasColumnType("uuid");

                    b.Property<string>("type")
                        .IsRequired()
                        .HasColumnType("varchar");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("file_id");

                    b.HasIndex("submission_id");

                    b.ToTable("submission_event", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_submission_event_message_length", "length(message) <= 1000");

                            t.HasCheckConstraint("CK_submission_event_type", "type IN ('Submitted', 'Registered', 'StatusChanged', 'InfoRequested', 'FileAdded', 'Completed', 'Rejected', 'Cancelled')");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.SubmissionFile", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("content_type")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("field_key")
                        .HasColumnType("text");

                    b.Property<string>("kind")
                        .IsRequired()
                        .HasColumnType("varchar");

                    b.Property<string>("original_name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("sha256")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("size_bytes")
                        .HasColumnType("bigint");

                    b.Property<string>("storage_key")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("submission_id")
                        .HasColumnType("uuid");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("submission_id");

                    b.ToTable("submission_file", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_submission_file_content_type_length", "length(content_type) <= 120");

                            t.HasCheckConstraint("CK_submission_file_field_key_length", "field_key IS NULL OR length(field_key) <= 60");

                            t.HasCheckConstraint("CK_submission_file_kind", "kind IN ('Application', 'Attachment', 'Response')");

                            t.HasCheckConstraint("CK_submission_file_original_name_length", "length(original_name) <= 255");

                            t.HasCheckConstraint("CK_submission_file_sha256_length", "length(sha256) <= 64");

                            t.HasCheckConstraint("CK_submission_file_storage_key_length", "length(storage_key) <= 200");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.Survey", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<bool>("allow_anonymous")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<string>("code")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("description")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset?>("ends_at")
                        .HasColumnType("timestamptz");

                    b.Property<bool>("is_published")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<bool>("show_results")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<DateTimeOffset?>("starts_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("code")
                        .IsUnique();

                    b.ToTable("survey", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_survey_code_length", "length(code) <= 50");

                            t.HasCheckConstraint("CK_survey_dates", "ends_at IS NULL OR starts_at IS NULL OR ends_at > starts_at");

                            t.HasCheckConstraint("CK_survey_title_length", "length(title) <= 250");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.SurveyQuestion", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<int>("display_order")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0);

                    b.Property<bool>("is_required")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<string>("key")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<JsonDocument>("options")
                        .HasColumnType("jsonb");

                    b.Property<Guid>("survey_id")
                        .HasColumnType("uuid");

                    b.Property<string>("text")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("type")
                        .IsRequired()
                        .HasColumnType("varchar");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("survey_id", "display_order");

                    b.HasIndex("survey_id", "key")
                        .IsUnique();

                    b.ToTable("survey_question", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_survey_question_display_order_non_negative", "display_order >= 0");

                            t.HasCheckConstraint("CK_survey_question_key_length", "length(key) <= 60");

                            t.HasCheckConstraint("CK_survey_question_text_length", "length(text) <= 1000");

                            t.HasCheckConstraint("CK_survey_question_type", "type IN ('SingleChoice', 'MultiChoice', 'Rating', 'FreeText')");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.SurveyResponse", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<JsonDocument>("answers")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<DateTimeOffset>("submitted_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid>("survey_id")
                        .HasColumnType("uuid");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid?>("user_id")
                        .HasColumnType("uuid");

                    b.HasKey("id");

                    b.HasIndex("user_id");

                    b.HasIndex("survey_id", "user_id")
                        .IsUnique()
                        .HasFilter("user_id IS NOT NULL");

                    b.ToTable("survey_response", "portal");
                });

            modelBuilder.Entity("API_PORTAL.Entities.User", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("address")
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("email")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("email_confirmed")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<int>("failed_login_count")
                        .HasColumnType("integer");

                    b.Property<string>("full_name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("is_active")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(true);

                    b.Property<DateTimeOffset?>("lockout_end")
                        .HasColumnType("timestamptz");

                    b.Property<string>("national_id")
                        .HasColumnType("text");

                    b.Property<string>("password_hash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("phone")
                        .HasColumnType("text");

                    b.Property<string>("role")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("varchar")
                        .HasDefaultValueSql("'Citizen'");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("email")
                        .IsUnique();

                    b.ToTable("user", "portal", t =>
                        {
                            t.HasCheckConstraint("CK_user_address_length", "address IS NULL OR length(address) <= 500");

                            t.HasCheckConstraint("CK_user_email_length", "length(email) <= 256");

                            t.HasCheckConstraint("CK_user_full_name_length", "length(full_name) <= 200");

                            t.HasCheckConstraint("CK_user_national_id_length", "national_id IS NULL OR length(national_id) <= 13");

                            t.HasCheckConstraint("CK_user_password_hash_length", "length(password_hash) <= 512");

                            t.HasCheckConstraint("CK_user_phone_length", "phone IS NULL OR length(phone) <= 30");

                            t.HasCheckConstraint("CK_user_role", "role IN ('Citizen', 'Clerk', 'Admin')");
                        });
                });

            modelBuilder.Entity("API_PORTAL.Entities.Appointment", b =>
                {
                    b.HasOne("API_PORTAL.Entities.User", "decided_by_user")
                        .WithMany()
                        .HasForeignKey("decided_by_user_id")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("API_PORTAL.Entities.AppointmentSlot", "slot")
                        .WithMany()
                        .HasForeignKey("slot_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("API_PORTAL.Entities.User", "user")
                        .WithMany()
                        .HasForeignKey("user_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("decided_by_user");

                    b.Navigation("slot");

                    b.Navigation("user");
                });

            modelBuilder.Entity("API_PORTAL.Entities.AppointmentSlot", b =>
                {
                    b.HasOne("API_PORTAL.Entities.AppointmentType", "appointment_type")
                        .WithMany()
                        .HasForeignKey("appointment_type_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("appointment_type");
                });

            modelBuilder.Entity("API_PORTAL.Entities.Notification", b =>
                {
                    b.HasOne("API_PORTAL.Entities.User", "user")
                        .WithMany()
                        .HasForeignKey("user_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("user");
                });

            modelBuilder.Entity("API_PORTAL.Entities.PublicRegistryDocument", b =>
                {
                    b.HasOne("API_PORTAL.Entities.PublicRegistryEntry", "entry")
                        .WithMany()
                        .HasForeignKey("entry_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("entry");
                });

            modelBuilder.Entity("API_PORTAL.Entities.PublicRegistryEntry", b =>
                {
                    b.HasOne("API_PORTAL.Entities.PublicRegistry", "registry")
                        .WithMany()
                        .HasForeignKey("registry_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("registry");
                });

            modelBuilder.Entity("API_PORTAL.Entities.RefreshToken", b =>
                {
                    b.HasOne("API_PORTAL.Entities.RefreshToken", "replaced_by")
                        .WithMany()
                        .HasForeignKey("replaced_by_id")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("API_PORTAL.Entities.User", "user")
                        .WithMany()
                        .HasForeignKey("user_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("replaced_by");

                    b.Navigation("user");
                });

            modelBuilder.Entity("API_PORTAL.Entities.ReportDefinition", b =>
                {
                    b.HasOne("API_PORTAL.Entities.User", "updated_by_user")
                        .WithMany()
                        .HasForeignKey("updated_by_user_id")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("updated_by_user");
                });

            modelBuilder.Entity("API_PORTAL.Entities.Submission", b =>
                {
                    b.HasOne("API_PORTAL.Entities.ServiceDefinition", "service")
                        .WithMany()
                        .HasForeignKey("service_id")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("API_PORTAL.Entities.User", "user")
                        .WithMany()
                        .HasForeignKey("user_id")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("service");

                    b.Navigation("user");
                });

            modelBuilder.Entity("API_PORTAL.Entities.SubmissionEvent", b =>
                {
                    b.HasOne("API_PORTAL.Entities.SubmissionFile", "file")
                        .WithMany()
                        .HasForeignKey("file_id")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("API_PORTAL.Entities.Submission", "submission")
                        .WithMany()
                        .HasForeignKey("submission_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("file");

                    b.Navigation("submission");
                });

            modelBuilder.Entity("API_PORTAL.Entities.SubmissionFile", b =>
                {
                    b.HasOne("API_PORTAL.Entities.Submission", "submission")
                        .WithMany()
                        .HasForeignKey("submission_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("submission");
                });

            modelBuilder.Entity("API_PORTAL.Entities.SurveyQuestion", b =>
                {
                    b.HasOne("API_PORTAL.Entities.Survey", "survey")
                        .WithMany()
                        .HasForeignKey("survey_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("survey");
                });

            modelBuilder.Entity("API_PORTAL.Entities.SurveyResponse", b =>
                {
                    b.HasOne("API_PORTAL.Entities.Survey", "survey")
                        .WithMany()
                        .HasForeignKey("survey_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("API_PORTAL.Entities.User", "user")
                        .WithMany()
                        .HasForeignKey("user_id")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.Navigation("survey");

                    b.Navigation("user");
                });
        }
    }
}
