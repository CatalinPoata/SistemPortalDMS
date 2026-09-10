using API_PORTAL.Auth;
using API_PORTAL.Data;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL_TESTS.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace API_PORTAL_TESTS.Tests
{
    public sealed class PublicSubmissionHttpTests
        : IClassFixture<PortalApiFactory>
    {
        private readonly PortalApiFactory factory;

        public PublicSubmissionHttpTests(PortalApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task Anonymous_catalog_contains_only_published_services()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var prefix = $"catalog-{Guid.NewGuid():N}";

            await SeedServiceAsync(
                $"{prefix}-published",
                isPublished: true,
                cancellationToken);
            await SeedServiceAsync(
                $"{prefix}-draft",
                isPublished: false,
                cancellationToken);

            var client = CreateAnonymousClient();
            using var response = await client.GetAsync(
                "/api/public/services?page=1&pageSize=100",
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            var codes = body.GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("code").GetString())
                .ToArray();

            Assert.Contains($"{prefix}-published", codes);
            Assert.DoesNotContain($"{prefix}-draft", codes);
        }

        [Fact]
        public async Task Unpublished_service_details_return_404_problem_details()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var code = $"hidden-{Guid.NewGuid():N}";

            await SeedServiceAsync(
                code,
                isPublished: false,
                cancellationToken);

            var client = CreateAnonymousClient();
            using var response = await client.GetAsync(
                $"/api/public/services/{code}",
                cancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task T8_Invalid_values_return_422_on_field_keys()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var code = $"validation-{Guid.NewGuid():N}";

            await SeedServiceAsync(
                code,
                isPublished: true,
                cancellationToken,
                maxLength: 5);

            var (client, _) = await CreateCitizenClientAsync(
                cancellationToken);

            await AssertInvalidValuesAsync(
                client,
                code,
                new { },
                "nume",
                cancellationToken);

            await AssertInvalidValuesAsync(
                client,
                code,
                new { nume = "Ion", necunoscut = "x" },
                "necunoscut",
                cancellationToken);

            await AssertInvalidValuesAsync(
                client,
                code,
                new { nume = "Valoare prea lungă" },
                "nume",
                cancellationToken);
        }

        [Fact]
        public async Task T9_Details_use_schema_snapshot_after_service_changes()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var code = $"snapshot-{Guid.NewGuid():N}";
            var serviceId = await SeedServiceAsync(
                code,
                isPublished: true,
                cancellationToken,
                maxLength: 5);
            var (client, _) = await CreateCitizenClientAsync(
                cancellationToken);

            using var createResponse = await client.PostAsJsonAsync(
                "/api/submissions",
                new
                {
                    serviceCode = code,
                    values = new { nume = "Ana" }
                },
                cancellationToken);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var created = await createResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            var submissionId = created.GetProperty("id").GetGuid();

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<PortalDbContext>();
                var service = await db.ServiceDefinitions.SingleAsync(
                    item => item.id == serviceId,
                    cancellationToken);

                service.form_schema = CreateSchema(maxLength: 100);
                service.schema_version = 2;
                await db.SaveChangesAsync(cancellationToken);
            }

            using var detailsResponse = await client.GetAsync(
                $"/api/submissions/{submissionId}",
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

            var details = await detailsResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            var snapshottedField = details.GetProperty("formSnapshot")
                .GetProperty("sections")[0]
                .GetProperty("fields")[0];

            Assert.Equal(1, details.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(5, snapshottedField.GetProperty("maxLength").GetInt32());
            Assert.Equal(
                "Submitted",
                details.GetProperty("events")[0]
                    .GetProperty("type")
                    .GetString());
        }

        [Fact]
        public async Task Submission_enqueues_registration_with_stable_metadata()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var code = $"outbox-{Guid.NewGuid():N}";
            await SeedServiceAsync(
                code,
                isPublished: true,
                cancellationToken);
            var (client, _) = await CreateCitizenClientAsync(cancellationToken);

            using var response = await client.PostAsJsonAsync(
                "/api/submissions",
                new
                {
                    serviceCode = code,
                    values = new { nume = "Cetățean Test" }
                },
                cancellationToken);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            var submissionId = created.GetProperty("id").GetGuid();
            var externalId = created.GetProperty("externalId").GetGuid();

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var message = await db.OutboxMessages.SingleAsync(item =>
                item.aggregate_id == submissionId,
                cancellationToken);

            Assert.Equal("Submission", message.aggregate_type);
            Assert.Equal("Submission.Register", message.event_type);
            Assert.Equal(OutboxMessageStatus.Pending, message.status);
            Assert.Equal(externalId, message.payload.RootElement
                .GetProperty("externalId").GetGuid());
            Assert.Equal("INTRARI", message.payload.RootElement
                .GetProperty("registryTypeCode").GetString());
            Assert.Equal("In", message.payload.RootElement
                .GetProperty("direction").GetString());
            Assert.Empty(message.payload.RootElement
                .GetProperty("documents").EnumerateArray());
        }

        [Fact]
        public async Task Citizen_cannot_read_another_citizens_submission()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var code = $"ownership-{Guid.NewGuid():N}";

            await SeedServiceAsync(
                code,
                isPublished: true,
                cancellationToken);

            var (ownerClient, _) = await CreateCitizenClientAsync(
                cancellationToken);
            var (otherClient, _) = await CreateCitizenClientAsync(
                cancellationToken);

            using var createResponse = await ownerClient.PostAsJsonAsync(
                "/api/submissions",
                new
                {
                    serviceCode = code,
                    values = new { nume = "Ion" }
                },
                cancellationToken);

            var created = await createResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            var submissionId = created.GetProperty("id").GetGuid();

            using var response = await otherClient.GetAsync(
                $"/api/submissions/{submissionId}",
                cancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Citizen_can_withdraw_submitted_request_once()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var code = $"withdraw-{Guid.NewGuid():N}";

            await SeedServiceAsync(
                code,
                isPublished: true,
                cancellationToken);

            var (client, _) = await CreateCitizenClientAsync(
                cancellationToken);

            using var createResponse = await client.PostAsJsonAsync(
                "/api/submissions",
                new
                {
                    serviceCode = code,
                    values = new { nume = "Mara" }
                },
                cancellationToken);
            var created = await createResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            var submissionId = created.GetProperty("id").GetGuid();

            using var first = await client.PostAsJsonAsync(
                $"/api/submissions/{submissionId}/withdraw",
                new { reason = "Nu mai este necesară." },
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var withdrawn = await first.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            Assert.Equal(
                "Cancelled",
                withdrawn.GetProperty("status").GetString());

            using var second = await client.PostAsJsonAsync(
                $"/api/submissions/{submissionId}/withdraw",
                new { reason = "Încercare repetată." },
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.UnprocessableEntity,
                second.StatusCode);
        }

        [Fact]
        public async Task Withdrawing_registered_request_enqueues_dms_cancellation()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var code = $"withdraw-dms-{Guid.NewGuid():N}";
            await SeedServiceAsync(code, true, cancellationToken);
            var (client, _) = await CreateCitizenClientAsync(cancellationToken);

            using var createResponse = await client.PostAsJsonAsync(
                "/api/submissions",
                new { serviceCode = code, values = new { nume = "Ana" } },
                cancellationToken);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            var submissionId = created.GetProperty("id").GetGuid();
            var entryId = Guid.NewGuid();

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
                var submission = await db.Submissions.SingleAsync(
                    item => item.id == submissionId,
                    cancellationToken);
                submission.status = SubmissionStatus.Registered;
                submission.dms_entry_id = entryId;
                await db.SaveChangesAsync(cancellationToken);
            }

            using var withdrawal = await client.PostAsJsonAsync(
                $"/api/submissions/{submissionId}/withdraw",
                new { reason = "Nu mai este necesară." },
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, withdrawal.StatusCode);

            var result = await withdrawal.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            Assert.Equal("Registered", result.GetProperty("status").GetString());
            Assert.Equal(
                "Retragerea a fost transmisă spre anulare în DMS.",
                result.GetProperty("statusDetails").GetString());

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
                var message = await db.OutboxMessages.SingleAsync(item =>
                    item.aggregate_id == submissionId &&
                    item.event_type == "Submission.Cancel",
                    cancellationToken);
                Assert.Equal(OutboxMessageStatus.Pending, message.status);
                Assert.Equal(entryId, message.payload.RootElement
                    .GetProperty("entryId").GetGuid());
                Assert.Equal("Nu mai este necesară.", message.payload.RootElement
                    .GetProperty("reason").GetString());
            }

            using var duplicate = await client.PostAsJsonAsync(
                $"/api/submissions/{submissionId}/withdraw",
                new { reason = "Cerere repetată." },
                cancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        }

        [Fact]
        public async Task Multipart_submission_stores_field_file_and_attachment()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var code = $"files-{Guid.NewGuid():N}";
            await SeedFileServiceAsync(
                code,
                requiresAttachment: true,
                acceptedType: ".pdf",
                cancellationToken);
            var (client, _) = await CreateCitizenClientAsync(
                cancellationToken);

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(code), "ServiceCode");
            content.Add(
                new StringContent("{\"nume\":\"Ana\"}"),
                "Values");
            content.Add(
                CreatePdfContent("cerere"),
                "Files",
                "dovada.pdf");
            content.Add(new StringContent("dovada"), "FileKeys");
            content.Add(
                CreatePdfContent("anexa"),
                "Files",
                "anexa.pdf");
            content.Add(
                new StringContent("__attachment__"),
                "FileKeys");

            using var response = await client.PostAsync(
                "/api/submissions/with-files",
                content,
                cancellationToken);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            var files = body.GetProperty("files").EnumerateArray().ToArray();
            var fieldFile = Assert.Single(files, item =>
                item.GetProperty("fieldKey").GetString() == "dovada");
            Assert.Single(files, item =>
                item.GetProperty("fieldKey").ValueKind ==
                    JsonValueKind.Null);
            Assert.Equal(
                fieldFile.GetProperty("id").GetGuid(),
                body.GetProperty("values")
                    .GetProperty("dovada")
                    .GetProperty("fileId")
                    .GetGuid());
            Assert.Equal(
                2,
                body.GetProperty("events")
                    .EnumerateArray()
                    .Count(item =>
                        item.GetProperty("type").GetString() ==
                            "FileAdded"));

            var submissionId = body.GetProperty("id").GetGuid();

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var stored = await db.SubmissionFiles
                .Where(item => item.submission_id == submissionId)
                .ToListAsync(cancellationToken);

            Assert.Equal(2, stored.Count);
            Assert.All(stored, item => Assert.True(File.Exists(
                Path.Combine(
                    factory.StorageRootPath,
                    item.storage_key.Replace('/', Path.DirectorySeparatorChar)))));
        }

        [Fact]
        public async Task Info_requested_submission_enqueues_clarification_files()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var code = $"clarifications-{Guid.NewGuid():N}";
            await SeedServiceAsync(code, true, cancellationToken);
            var (client, _) = await CreateCitizenClientAsync(cancellationToken);

            using var createResponse = await client.PostAsJsonAsync(
                "/api/submissions",
                new { serviceCode = code, values = new { nume = "Ana" } },
                cancellationToken);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            var submissionId = created.GetProperty("id").GetGuid();

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
                var submission = await db.Submissions.SingleAsync(
                    item => item.id == submissionId,
                    cancellationToken);
                submission.status = SubmissionStatus.InfoRequested;
                submission.dms_entry_id = Guid.NewGuid();
                await db.SaveChangesAsync(cancellationToken);
            }

            using var content = new MultipartFormDataContent();
            content.Add(
                CreatePdfContent("clarificare"),
                "Files",
                "clarificare.pdf");
            using var response = await client.PostAsync(
                $"/api/submissions/{submissionId}/clarifications",
                content,
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await using var verificationScope = factory.Services
                .CreateAsyncScope();
            var verificationDb = verificationScope.ServiceProvider
                .GetRequiredService<PortalDbContext>();
            var outbox = await verificationDb.OutboxMessages.SingleAsync(
                item => item.aggregate_id == submissionId &&
                    item.event_type == "Submission.AddDocuments",
                cancellationToken);
            Assert.Equal(OutboxMessageStatus.Pending, outbox.status);
            Assert.Equal("ANEXA", outbox.payload.RootElement
                .GetProperty("documents")[0]
                .GetProperty("documentKindCode").GetString());

            var file = await verificationDb.SubmissionFiles.SingleAsync(
                item => item.submission_id == submissionId,
                cancellationToken);
            Assert.Equal(SubmissionFileKind.Attachment, file.kind);
        }

        [Fact]
        public async Task Multipart_submission_rejects_content_outside_field_accept()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var code = $"file-type-{Guid.NewGuid():N}";
            var serviceId = await SeedFileServiceAsync(
                code,
                requiresAttachment: false,
                acceptedType: ".png",
                cancellationToken);
            var (client, _) = await CreateCitizenClientAsync(
                cancellationToken);

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(code), "ServiceCode");
            content.Add(
                new StringContent("{\"nume\":\"Ana\"}"),
                "Values");
            content.Add(
                CreatePdfContent("continut-pdf"),
                "Files",
                "imagine.png");
            content.Add(new StringContent("dovada"), "FileKeys");

            using var response = await client.PostAsync(
                "/api/submissions/with-files",
                content,
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.UnprocessableEntity,
                response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            Assert.True(problem.GetProperty("errors")
                .TryGetProperty("dovada", out _));

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            Assert.False(await db.Submissions.AnyAsync(
                item => item.service_id == serviceId,
                cancellationToken));
        }

        [Fact]
        public async Task Multipart_file_over_10_mb_returns_413_problem_details()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var code = $"file-size-{Guid.NewGuid():N}";
            await SeedFileServiceAsync(
                code,
                requiresAttachment: false,
                acceptedType: ".pdf",
                cancellationToken);
            var (client, _) = await CreateCitizenClientAsync(
                cancellationToken);
            var bytes = new byte[10 * 1024 * 1024 + 1];
            "%PDF-"u8.CopyTo(bytes);

            using var file = new ByteArrayContent(bytes);
            file.Headers.ContentType =
                new MediaTypeHeaderValue("application/pdf");
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(code), "ServiceCode");
            content.Add(
                new StringContent("{\"nume\":\"Ana\"}"),
                "Values");
            content.Add(file, "Files", "prea-mare.pdf");
            content.Add(new StringContent("dovada"), "FileKeys");

            using var response = await client.PostAsync(
                "/api/submissions/with-files",
                content,
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.RequestEntityTooLarge,
                response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task Submission_file_download_is_signed_and_owner_scoped()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var code = $"file-download-{Guid.NewGuid():N}";
            await SeedFileServiceAsync(
                code,
                requiresAttachment: false,
                acceptedType: ".pdf",
                cancellationToken);
            var (ownerClient, _) = await CreateCitizenClientAsync(
                cancellationToken);
            var (otherClient, _) = await CreateCitizenClientAsync(
                cancellationToken);

            using var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent(code), "ServiceCode");
            multipart.Add(
                new StringContent("{\"nume\":\"Ana\"}"),
                "Values");
            multipart.Add(
                CreatePdfContent("continut-descarcat"),
                "Files",
                "dovada.pdf");
            multipart.Add(new StringContent("dovada"), "FileKeys");

            using var createResponse = await ownerClient.PostAsync(
                "/api/submissions/with-files",
                multipart,
                cancellationToken);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var created = await createResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            var submissionId = created.GetProperty("id").GetGuid();
            var fileId = created.GetProperty("files")[0]
                .GetProperty("id")
                .GetGuid();
            var endpoint =
                $"/api/submissions/{submissionId}/files/{fileId}/download-url";

            using var forbidden = await otherClient.PostAsync(
                endpoint,
                content: null,
                cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

            using var signedUrlResponse = await ownerClient.PostAsync(
                endpoint,
                content: null,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, signedUrlResponse.StatusCode);

            var signed = await signedUrlResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            var signedUrl = signed.GetProperty("url").GetString();
            Assert.NotNull(signedUrl);
            Assert.Equal(
                fileId,
                signed.GetProperty("fileId").GetGuid());

            var anonymousClient = CreateAnonymousClient();
            using var download = await anonymousClient.GetAsync(
                signedUrl,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal(
                "application/pdf",
                download.Content.Headers.ContentType?.MediaType);
            Assert.Equal(
                "%PDF-1.7\ncontinut-descarcat",
                await download.Content.ReadAsStringAsync(cancellationToken));

            using var invalidToken = await anonymousClient.GetAsync(
                signedUrl + "x",
                cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, invalidToken.StatusCode);
            Assert.Equal(
                "application/problem+json",
                invalidToken.Content.Headers.ContentType?.MediaType);
        }

        private async Task<Guid> SeedServiceAsync(
            string code,
            bool isPublished,
            CancellationToken cancellationToken,
            int maxLength = 200)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var service = new ServiceDefinition
            {
                id = Guid.NewGuid(),
                code = code,
                title = $"Serviciu {code}",
                short_description = "Descriere publică",
                description = "<p>Descriere sigură</p>",
                registry_type_code = "INTRARI",
                form_schema = CreateSchema(maxLength),
                schema_version = 1,
                requires_attachment = false,
                max_attachments = 0,
                is_published = isPublished,
                display_order = 0
            };

            db.ServiceDefinitions.Add(service);
            await db.SaveChangesAsync(cancellationToken);

            return service.id;
        }

        private async Task<Guid> SeedFileServiceAsync(
            string code,
            bool requiresAttachment,
            string acceptedType,
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var service = new ServiceDefinition
            {
                id = Guid.NewGuid(),
                code = code,
                title = $"Serviciu {code}",
                short_description = "Serviciu cu fișiere",
                description = "<p>Descriere sigură</p>",
                registry_type_code = "INTRARI",
                form_schema = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    sections = new[]
                    {
                        new
                        {
                            key = "solicitant",
                            title = "Date solicitant",
                            fields = new object[]
                            {
                                new
                                {
                                    key = "nume",
                                    label = "Nume complet",
                                    type = "text",
                                    required = true,
                                    maxLength = 200
                                },
                                new
                                {
                                    key = "dovada",
                                    label = "Dovadă",
                                    type = "file",
                                    required = true,
                                    maxSizeMb = 2,
                                    accept = new[] { acceptedType }
                                }
                            }
                        }
                    }
                })),
                schema_version = 1,
                requires_attachment = requiresAttachment,
                max_attachments = requiresAttachment ? 2 : 0,
                is_published = true,
                display_order = 0
            };

            db.ServiceDefinitions.Add(service);
            await db.SaveChangesAsync(cancellationToken);

            return service.id;
        }

        private static ByteArrayContent CreatePdfContent(string text)
        {
            var content = new ByteArrayContent(
                System.Text.Encoding.UTF8.GetBytes($"%PDF-1.7\n{text}"));
            content.Headers.ContentType =
                new MediaTypeHeaderValue("application/pdf");

            return content;
        }

        private async Task<(HttpClient Client, User User)>
            CreateCitizenClientAsync(CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var tokenService = scope.ServiceProvider
                .GetRequiredService<IJwtTokenService>();
            var user = new User
            {
                id = Guid.NewGuid(),
                email = $"citizen-{Guid.NewGuid():N}@example.com",
                full_name = "Cetățean Test",
                role = Role.Citizen,
                email_confirmed = true,
                is_active = true,
                password_hash = "unused"
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);

            var client = CreateAnonymousClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    tokenService.Create(user).AccessToken);

            return (client, user);
        }

        private HttpClient CreateAnonymousClient()
        {
            return factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });
        }

        private static async Task AssertInvalidValuesAsync(
            HttpClient client,
            string serviceCode,
            object values,
            string errorKey,
            CancellationToken cancellationToken)
        {
            using var response = await client.PostAsJsonAsync(
                "/api/submissions",
                new { serviceCode, values },
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.UnprocessableEntity,
                response.StatusCode);

            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);

            Assert.True(
                problem.GetProperty("errors").TryGetProperty(errorKey, out _));
        }

        private static JsonDocument CreateSchema(int maxLength)
        {
            return JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                sections = new[]
                {
                    new
                    {
                        key = "solicitant",
                        title = "Date solicitant",
                        fields = new[]
                        {
                            new
                            {
                                key = "nume",
                                label = "Nume complet",
                                type = "text",
                                required = true,
                                maxLength
                            }
                        }
                    }
                }
            }));
        }
    }
}
