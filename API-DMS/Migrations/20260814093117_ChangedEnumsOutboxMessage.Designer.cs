using System;
using System.Text.Json;
using API_DMS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;


namespace API_DMS.Migrations
{
    [DbContext(typeof(DmsDbContext))]
    [Migration("20260814093117_ChangedEnumsOutboxMessage")]
    partial class ChangedEnumsOutboxMessage
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasDefaultSchema("dms")
                .HasAnnotation("ProductVersion", "9.0.19")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("API_DMS.Entities.Department", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("code")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<bool>("is_active")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(true);

                    b.Property<Guid?>("manager_user_id")
                        .HasColumnType("uuid");

                    b.Property<string>("name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("code")
                        .IsUnique();

                    b.HasIndex("manager_user_id");

                    b.ToTable("department", "dms", t =>
                        {
                            t.HasCheckConstraint("CK_department_code_length", "length(code) <= 20");

                            t.HasCheckConstraint("CK_department_name_length", "length(name) <= 200");
                        });
                });

            modelBuilder.Entity("API_DMS.Entities.DocumentKind", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("code")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<bool>("is_active")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(true);

                    b.Property<string>("name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("code")
                        .IsUnique();

                    b.ToTable("document_kind", "dms", t =>
                        {
                            t.HasCheckConstraint("CK_document_kind_code_length", "length(code) <= 30");

                            t.HasCheckConstraint("CK_document_kind_name_length", "length(name) <= 150");
                        });
                });

            modelBuilder.Entity("API_DMS.Entities.EntryEvent", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid?>("actor_user_id")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid>("entry_id")
                        .HasColumnType("uuid");

                    b.Property<string>("message")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("occurred_at")
                        .HasColumnType("timestamptz");

                    b.Property<JsonDocument>("payload")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<string>("type")
                        .IsRequired()
                        .HasColumnType("varchar");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("actor_user_id");

                    b.HasIndex("entry_id");

                    b.ToTable("entry_event", "dms", t =>
                        {
                            t.HasCheckConstraint("CK_entry_event_message_length", "length(message) <= 1000");

                            t.HasCheckConstraint("CK_entry_event_type", "type IN ('Created', 'StatusChanged', 'Assigned', 'DocumentAdded', 'TaskCompleted')");
                        });
                });

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

                    b.ToTable("inbound_request", "dms", t =>
                        {
                            t.HasCheckConstraint("CK_inbound_request_endpoint_length", "length(endpoint) <= 100");

                            t.HasCheckConstraint("CK_inbound_request_hash_length", "length(request_hash) = 64");

                            t.HasCheckConstraint("CK_inbound_request_idempotency_key_length", "length(idempotency_key) <= 100");

                            t.HasCheckConstraint("CK_inbound_request_response_status", "response_status >= 100 AND response_status <= 599");
                        });
                });

            modelBuilder.Entity("API_DMS.Entities.InboxEvent", b =>
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

                    b.ToTable("inbox_event", "dms", t =>
                        {
                            t.HasCheckConstraint("CK_inbox_event_event_type_length", "length(event_type) <= 80");

                            t.HasCheckConstraint("CK_inbox_event_source_length", "length(source) <= 40");
                        });
                });

            modelBuilder.Entity("API_DMS.Entities.OutboxMessage", b =>
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

                    b.ToTable("outbox_message", "dms", t =>
                        {
                            t.HasCheckConstraint("CK_outbox_message_aggregate_type_length", "length(aggregate_type) <= 60");

                            t.HasCheckConstraint("CK_outbox_message_attempts_nonnegative", "attempts >= 0");

                            t.HasCheckConstraint("CK_outbox_message_event_type_length", "length(event_type) <= 80");

                            t.HasCheckConstraint("CK_outbox_message_last_error_length", "last_error IS NULL OR length(last_error) <= 2000");

                            t.HasCheckConstraint("CK_outbox_message_status", "status IN ('Pending', 'Delivered', 'Failed')");
                        });
                });

            modelBuilder.Entity("API_DMS.Entities.RefreshToken", b =>
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

                    b.ToTable("refresh_token", "dms", t =>
                        {
                            t.HasCheckConstraint("CK_refresh_token_token_hash_length", "length(token_hash) <= 128");
                        });
                });

            modelBuilder.Entity("API_DMS.Entities.RegistryDocument", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("content_type")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<string>("direction")
                        .IsRequired()
                        .HasColumnType("varchar");

                    b.Property<DateOnly?>("document_date")
                        .HasColumnType("date");

                    b.Property<Guid>("document_kind_id")
                        .HasColumnType("uuid");

                    b.Property<Guid>("entry_id")
                        .HasColumnType("uuid");

                    b.Property<Guid?>("external_file_id")
                        .HasColumnType("uuid");

                    b.Property<string>("issuer")
                        .HasColumnType("text");

                    b.Property<string>("note")
                        .HasColumnType("text");

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

                    b.Property<Guid>("uploaded_by_user_id")
                        .HasColumnType("uuid");

                    b.HasKey("id");

                    b.HasIndex("document_kind_id");

                    b.HasIndex("entry_id");

                    b.HasIndex("external_file_id")
                        .IsUnique();

                    b.HasIndex("uploaded_by_user_id");

                    b.ToTable("registry_document", "dms", t =>
                        {
                            t.HasCheckConstraint("CK_registry_document_content_type_length", "length(content_type) <= 120");

                            t.HasCheckConstraint("CK_registry_document_direction", "direction IN ('In', 'Out')");

                            t.HasCheckConstraint("CK_registry_document_issuer_length", "issuer IS NULL OR length(issuer) <= 200");

                            t.HasCheckConstraint("CK_registry_document_note_length", "note IS NULL OR length(note) <= 500");

                            t.HasCheckConstraint("CK_registry_document_original_name_length", "length(original_name) <= 255");

                            t.HasCheckConstraint("CK_registry_document_sha256_length", "length(sha256) <= 64");

                            t.HasCheckConstraint("CK_registry_document_storage_key_length", "length(storage_key) <= 200");
                        });
                });

            modelBuilder.Entity("API_DMS.Entities.RegistryEntry", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("applicant_address")
                        .HasColumnType("text");

                    b.Property<string>("applicant_email")
                        .HasColumnType("text");

                    b.Property<string>("applicant_name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("applicant_national_id")
                        .HasColumnType("text");

                    b.Property<string>("applicant_phone")
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid>("created_by_user_id")
                        .HasColumnType("uuid");

                    b.Property<DateOnly>("deadline")
                        .HasColumnType("date");

                    b.Property<Guid?>("department_id")
                        .HasColumnType("uuid");

                    b.Property<string>("direction")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("varchar")
                        .HasDefaultValueSql("'In'");

                    b.Property<Guid?>("external_id")
                        .HasColumnType("uuid");

                    b.Property<JsonDocument>("form_values")
                        .HasColumnType("jsonb");

                    b.Property<long>("number")
                        .HasColumnType("bigint");

                    b.Property<DateTimeOffset>("registered_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid>("registry_type_id")
                        .HasColumnType("uuid");

                    b.Property<string>("service_code")
                        .HasColumnType("text");

                    b.Property<DateOnly?>("source_doc_date")
                        .HasColumnType("date");

                    b.Property<string>("source_doc_number")
                        .HasColumnType("text");

                    b.Property<string>("status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("varchar")
                        .HasDefaultValueSql("'Registered'");

                    b.Property<string>("status_note")
                        .HasColumnType("text");

                    b.Property<string>("subject")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset?>("submitted_at")
                        .HasColumnType("timestamptz");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.Property<int>("year")
                        .HasColumnType("integer");

                    b.HasKey("id");

                    b.HasIndex("applicant_name");

                    b.HasIndex("created_by_user_id");

                    b.HasIndex("department_id");

                    b.HasIndex("external_id")
                        .IsUnique();

                    b.HasIndex("status");

                    b.HasIndex("subject");

                    b.HasIndex("registry_type_id", "year", "number")
                        .IsUnique();

                    b.HasIndex("registry_type_id", "year", "registered_at");

                    b.ToTable("registry_entry", "dms", t =>
                        {
                            t.HasCheckConstraint("CK_registry_entry_applicant_address_length", "applicant_address IS NULL OR length(applicant_address) <= 500");

                            t.HasCheckConstraint("CK_registry_entry_applicant_email_length", "applicant_email IS NULL OR length(applicant_email) <= 256");

                            t.HasCheckConstraint("CK_registry_entry_applicant_name_length", "length(applicant_name) <= 200");

                            t.HasCheckConstraint("CK_registry_entry_applicant_national_id_length", "applicant_national_id IS NULL OR length(applicant_national_id) <= 13");

                            t.HasCheckConstraint("CK_registry_entry_applicant_phone_length", "applicant_phone IS NULL OR length(applicant_phone) <= 30");

                            t.HasCheckConstraint("CK_registry_entry_direction", "direction IN ('In', 'Out')");

                            t.HasCheckConstraint("CK_registry_entry_service_code_length", "service_code IS NULL OR length(service_code) <= 50");

                            t.HasCheckConstraint("CK_registry_entry_source_doc_number_length", "source_doc_number IS NULL OR length(source_doc_number) <= 60");

                            t.HasCheckConstraint("CK_registry_entry_status", "status IN ('Submitted', 'Registered', 'InReview', 'InfoRequested', 'Completed', 'Rejected', 'Cancelled')");

                            t.HasCheckConstraint("CK_registry_entry_status_note_length", "status_note IS NULL OR length(status_note) <= 1000");

                            t.HasCheckConstraint("CK_registry_entry_subject_length", "length(subject) <= 1000");
                        });
                });

            modelBuilder.Entity("API_DMS.Entities.RegistryNumberCounter", b =>
                {
                    b.Property<Guid>("registry_type_id")
                        .HasColumnType("uuid");

                    b.Property<int>("year")
                        .HasColumnType("integer");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<long>("last_number")
                        .HasColumnType("bigint");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("registry_type_id", "year");

                    b.ToTable("registry_number_counter", "dms");
                });

            modelBuilder.Entity("API_DMS.Entities.RegistryType", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("code")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<int>("default_deadline_days")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(30);

                    b.Property<string>("direction")
                        .IsRequired()
                        .HasColumnType("varchar");

                    b.Property<bool>("is_closed")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<string>("name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("start_number")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasDefaultValue(1L);

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("code")
                        .IsUnique();

                    b.ToTable("registry_type", "dms", t =>
                        {
                            t.HasCheckConstraint("CK_registry_type_code_length", "length(code) <= 30");

                            t.HasCheckConstraint("CK_registry_type_direction", "direction IN ('In', 'Out', 'Both')");

                            t.HasCheckConstraint("CK_registry_type_name_length", "length(name) <= 200");
                        });
                });

            modelBuilder.Entity("API_DMS.Entities.ReportDefinition", b =>
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

                    b.ToTable("report_definition", "dms", t =>
                        {
                            t.HasCheckConstraint("CK_report_definition_code_length", "length(code) <= 50");

                            t.HasCheckConstraint("CK_report_definition_dataset_key_length", "length(dataset_key) <= 50");

                            t.HasCheckConstraint("CK_report_definition_name_length", "length(name) <= 200");

                            t.HasCheckConstraint("CK_report_definition_version_positive", "version >= 1");
                        });
                });

            modelBuilder.Entity("API_DMS.Entities.Task", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid?>("assignee_user_id")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset?>("completed_at")
                        .HasColumnType("timestamptz");

                    b.Property<DateTime>("created_at")
                        .HasColumnType("timestamptz");

                    b.Property<Guid>("created_by_user_id")
                        .HasColumnType("uuid");

                    b.Property<Guid?>("department_id")
                        .HasColumnType("uuid");

                    b.Property<DateOnly?>("due_date")
                        .HasColumnType("date");

                    b.Property<Guid>("entry_id")
                        .HasColumnType("uuid");

                    b.Property<string>("instructions")
                        .HasColumnType("text");

                    b.Property<string>("resolution_note")
                        .HasColumnType("text");

                    b.Property<string>("status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("varchar")
                        .HasDefaultValueSql("'Open'");

                    b.Property<string>("title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("updated_at")
                        .HasColumnType("timestamptz");

                    b.HasKey("id");

                    b.HasIndex("assignee_user_id");

                    b.HasIndex("created_by_user_id");

                    b.HasIndex("department_id");

                    b.HasIndex("entry_id");

                    b.ToTable("task", "dms", t =>
                        {
                            t.HasCheckConstraint("CK_task_assignee_or_department", "assignee_user_id IS NOT NULL OR department_id IS NOT NULL");

                            t.HasCheckConstraint("CK_task_resolution_note_length", "resolution_note IS NULL OR length(resolution_note) <= 1000");

                            t.HasCheckConstraint("CK_task_status", "status IN ('Open', 'Done', 'Cancelled')");

                            t.HasCheckConstraint("CK_task_title_length", "length(title) <= 200");
                        });
                });

            modelBuilder.Entity("API_DMS.Entities.User", b =>
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

                    b.ToTable("user", "dms", t =>
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

            modelBuilder.Entity("API_DMS.Entities.Department", b =>
                {
                    b.HasOne("API_DMS.Entities.User", "manager_user")
                        .WithMany()
                        .HasForeignKey("manager_user_id")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.Navigation("manager_user");
                });

            modelBuilder.Entity("API_DMS.Entities.EntryEvent", b =>
                {
                    b.HasOne("API_DMS.Entities.User", "actor_user")
                        .WithMany()
                        .HasForeignKey("actor_user_id")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("API_DMS.Entities.RegistryEntry", "entry")
                        .WithMany()
                        .HasForeignKey("entry_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("actor_user");

                    b.Navigation("entry");
                });

            modelBuilder.Entity("API_DMS.Entities.RefreshToken", b =>
                {
                    b.HasOne("API_DMS.Entities.RefreshToken", "replaced_by")
                        .WithMany()
                        .HasForeignKey("replaced_by_id")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("API_DMS.Entities.User", "user")
                        .WithMany()
                        .HasForeignKey("user_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("replaced_by");

                    b.Navigation("user");
                });

            modelBuilder.Entity("API_DMS.Entities.RegistryDocument", b =>
                {
                    b.HasOne("API_DMS.Entities.DocumentKind", "document_kind")
                        .WithMany()
                        .HasForeignKey("document_kind_id")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("API_DMS.Entities.RegistryEntry", "entry")
                        .WithMany()
                        .HasForeignKey("entry_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("API_DMS.Entities.User", "uploaded_by_user")
                        .WithMany()
                        .HasForeignKey("uploaded_by_user_id")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("document_kind");

                    b.Navigation("entry");

                    b.Navigation("uploaded_by_user");
                });

            modelBuilder.Entity("API_DMS.Entities.RegistryEntry", b =>
                {
                    b.HasOne("API_DMS.Entities.User", "created_by_user")
                        .WithMany()
                        .HasForeignKey("created_by_user_id")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("API_DMS.Entities.Department", "department")
                        .WithMany()
                        .HasForeignKey("department_id")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("API_DMS.Entities.RegistryType", "registry_type")
                        .WithMany()
                        .HasForeignKey("registry_type_id")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("created_by_user");

                    b.Navigation("department");

                    b.Navigation("registry_type");
                });

            modelBuilder.Entity("API_DMS.Entities.RegistryNumberCounter", b =>
                {
                    b.HasOne("API_DMS.Entities.RegistryType", "registry_type")
                        .WithMany()
                        .HasForeignKey("registry_type_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("registry_type");
                });

            modelBuilder.Entity("API_DMS.Entities.ReportDefinition", b =>
                {
                    b.HasOne("API_DMS.Entities.User", "updated_by_user")
                        .WithMany()
                        .HasForeignKey("updated_by_user_id")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("updated_by_user");
                });

            modelBuilder.Entity("API_DMS.Entities.Task", b =>
                {
                    b.HasOne("API_DMS.Entities.User", "assignee_user")
                        .WithMany()
                        .HasForeignKey("assignee_user_id")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("API_DMS.Entities.User", "created_by_user")
                        .WithMany()
                        .HasForeignKey("created_by_user_id")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("API_DMS.Entities.Department", "department")
                        .WithMany()
                        .HasForeignKey("department_id")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("API_DMS.Entities.RegistryEntry", "entry")
                        .WithMany()
                        .HasForeignKey("entry_id")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("assignee_user");

                    b.Navigation("created_by_user");

                    b.Navigation("department");

                    b.Navigation("entry");
                });
        }
    }
}
