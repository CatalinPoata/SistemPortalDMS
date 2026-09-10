using API_DMS.Data;
using API_DMS.Entities;
using API_DMS.Entities.Base;
using API_DMS_TESTS.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Sdk;
using Task = System.Threading.Tasks.Task;

namespace API_DMS_TESTS.Tests
{
    public sealed class ServiceIntegrationHttpTests
        : IClassFixture<DmsApiFactory>
    {
        private readonly DmsApiFactory factory;

        public ServiceIntegrationHttpTests(DmsApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task Signed_registry_lookup_returns_registry_state()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var code = $"INT{Guid.NewGuid():N}"[..30].ToUpperInvariant();

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<DmsDbContext>();

                db.RegistryTypes.Add(new RegistryType
                {
                    id = Guid.NewGuid(),
                    code = code,
                    name = "Registru integrare",
                    direction = RegistryDirection.In,
                    start_number = 1,
                    default_deadline_days = 30,
                    is_closed = false
                });

                await db.SaveChangesAsync(cancellationToken);
            }

            using var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false
                });
            using var request = CreateSignedRequest(
                HttpMethod.Get,
                $"/api/integration/registry-types/{code}");
            using var response = await client.SendAsync(
                request,
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var payload = await response.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);

            Assert.Equal(code, payload.GetProperty("code").GetString());
            Assert.False(payload.GetProperty("isClosed").GetBoolean());
        }

        [Fact]
        public async Task Unsigned_registry_lookup_returns_401_problem_details()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var client = factory.CreateClient();
            using var response = await client.GetAsync(
                "/api/integration/registry-types/INTRARI",
                cancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task Signed_registration_is_idempotent_and_allocates_once()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var registryCode = $"P{Guid.NewGuid():N}"[..30].ToUpperInvariant();

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<DmsDbContext>();

                await GetOrCreateIntegrationActorAsync(db, cancellationToken);
                db.RegistryTypes.Add(new RegistryType
                {
                    id = Guid.NewGuid(),
                    code = registryCode,
                    name = "Registru Portal",
                    direction = RegistryDirection.In,
                    start_number = 1,
                    default_deadline_days = 30,
                    is_closed = false
                });

                await db.SaveChangesAsync(cancellationToken);
            }

            var externalId = Guid.NewGuid();
            var payload = JsonSerializer.Serialize(new
            {
                externalId,
                registryTypeCode = registryCode,
                serviceCode = "serviciu-test",
                direction = "In",
                subject = "Cerere din portal",
                applicant = new
                {
                    name = "Cetățean Test",
                    email = "cetatean@example.com"
                },
                submittedAt = DateTimeOffset.UtcNow,
                formValues = new { nume = "Cetățean Test" },
                documents = Array.Empty<object>()
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var idempotencyKey = Guid.NewGuid().ToString();
            using var client = factory.CreateClient();

            using var firstRequest = CreateSignedRequest(
                HttpMethod.Post,
                "/api/integration/registry-entries",
                payload,
                idempotencyKey);
            using var firstResponse = await client.SendAsync(
                firstRequest,
                cancellationToken);

            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
            var firstBody = await firstResponse.Content.ReadAsStringAsync(
                cancellationToken);

            using var retryRequest = CreateSignedRequest(
                HttpMethod.Post,
                "/api/integration/registry-entries",
                payload,
                idempotencyKey);
            using var retryResponse = await client.SendAsync(
                retryRequest,
                cancellationToken);

            Assert.Equal(HttpStatusCode.Created, retryResponse.StatusCode);
            Assert.Equal(firstBody, await retryResponse.Content.ReadAsStringAsync(
                cancellationToken));

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<DmsDbContext>();
                Assert.Equal(1, await db.RegistryEntries.CountAsync(item =>
                    item.external_id == externalId,
                    cancellationToken));
                Assert.Equal(1, await db.InboundRequests.CountAsync(item =>
                    item.endpoint == "/api/integration/registry-entries" &&
                    item.idempotency_key == idempotencyKey,
                    cancellationToken));
            }

            var changedPayload = payload.Replace(
                "Cerere din portal",
                "Cerere modificată");
            using var conflictRequest = CreateSignedRequest(
                HttpMethod.Post,
                "/api/integration/registry-entries",
                changedPayload,
                idempotencyKey);
            using var conflictResponse = await client.SendAsync(
                conflictRequest,
                cancellationToken);

            Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        }

        [Fact]
        [Trait("Category", "Postgres")]
        public async Task T2_Parallel_same_idempotency_key_creates_one_entry()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var connection = Environment.GetEnvironmentVariable(
                "DMS_TEST_CONNECTION");

            if (string.IsNullOrWhiteSpace(connection))
            {
                throw SkipException.ForSkip(
                    "DMS_TEST_CONNECTION nu este configurată.");
            }

            using var postgresFactory = new DmsApiFactory(connection);
            var registry = new RegistryType
            {
                id = Guid.NewGuid(),
                code = $"T2{Guid.NewGuid():N}"[..30].ToUpperInvariant(),
                name = "Registru paralel T2",
                direction = RegistryDirection.In,
                start_number = 1,
                default_deadline_days = 30,
                is_closed = false
            };

            await using (var scope = postgresFactory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
                await GetOrCreateIntegrationActorAsync(db, cancellationToken);
                db.RegistryTypes.Add(registry);
                await db.SaveChangesAsync(cancellationToken);
            }

            var externalId = Guid.NewGuid();
            var payload = JsonSerializer.Serialize(new
            {
                externalId,
                registryTypeCode = registry.code,
                serviceCode = "serviciu-t2",
                direction = "In",
                subject = "Cerere paralelă",
                applicant = new { name = "Cetățean T2", email = "t2@example.com" },
                submittedAt = DateTimeOffset.UtcNow,
                formValues = new { nume = "Cetățean T2" },
                documents = Array.Empty<object>()
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var idempotencyKey = Guid.NewGuid().ToString("N");

            var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(
                async _ =>
                {
                    using var client = postgresFactory.CreateClient();
                    using var request = CreateSignedRequest(
                        HttpMethod.Post,
                        "/api/integration/registry-entries",
                        payload,
                        idempotencyKey);
                    using var response = await client.SendAsync(
                        request,
                        cancellationToken);

                    return (
                        response.StatusCode,
                        await response.Content.ReadAsStringAsync(cancellationToken));
                }));

            Assert.All(results, result =>
                Assert.Equal(HttpStatusCode.Created, result.StatusCode));
            Assert.Equal(results[0].Item2, results[1].Item2);

            await using var verificationScope =
                postgresFactory.Services.CreateAsyncScope();
            var verificationDb = verificationScope.ServiceProvider
                .GetRequiredService<DmsDbContext>();
            var entry = await verificationDb.RegistryEntries.SingleAsync(
                item => item.external_id == externalId,
                cancellationToken);
            var counter = await verificationDb.RegistryNumberCounters.SingleAsync(
                item => item.registry_type_id == registry.id &&
                    item.year == entry.year,
                cancellationToken);

            Assert.Equal(1, entry.number);
            Assert.Equal(entry.number, counter.last_number);
            Assert.Equal(1, await verificationDb.InboundRequests.CountAsync(
                item => item.endpoint == "/api/integration/registry-entries" &&
                    item.idempotency_key == idempotencyKey,
                cancellationToken));
        }

        [Fact]
        public async Task Clarifications_are_idempotent_and_return_entry_to_review()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var externalId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var fileId = Guid.NewGuid();
            var content = Encoding.UTF8.GetBytes("%PDF-1.7\nclarificare");
            var sha256 = Convert.ToHexString(SHA256.HashData(content))
                .ToLowerInvariant();
            factory.PortalFiles.SetFile(fileId, content, "application/pdf");

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
                var actor = await GetOrCreateIntegrationActorAsync(
                    db,
                    cancellationToken);
                var kind = new DocumentKind
                {
                    id = Guid.NewGuid(),
                    code = "ANEXA",
                    name = "Anexă",
                    is_active = true
                };
                var entry = new RegistryEntry
                {
                    id = entryId,
                    external_id = externalId,
                    registry_type_id = Guid.NewGuid(),
                    year = 2026,
                    number = 1,
                    direction = EntryDirection.In,
                    registered_at = DateTimeOffset.UtcNow,
                    subject = "Clarificare test",
                    applicant_name = "Cetățean Test",
                    deadline = DateOnly.FromDateTime(DateTime.UtcNow),
                    status = EntryStatus.InfoRequested,
                    status_note = "Este necesară o completare.",
                    created_by_user_id = actor.id
                };

                db.AddRange(kind, entry);
                await db.SaveChangesAsync(cancellationToken);
            }

            var body = JsonSerializer.Serialize(new
            {
                externalId,
                entryId,
                documents = new[]
                {
                    new
                    {
                        fileId,
                        name = "clarificare.pdf",
                        contentType = "application/pdf",
                        sizeBytes = content.LongLength,
                        sha256,
                        documentKindCode = "ANEXA"
                    }
                }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var key = Guid.NewGuid().ToString();
            using var client = factory.CreateClient();

            using var firstRequest = CreateSignedRequest(
                HttpMethod.Post,
                $"/api/integration/registry-entries/{entryId}/documents",
                body,
                key);
            using var firstResponse = await client.SendAsync(
                firstRequest,
                cancellationToken);
            var firstBody = await firstResponse.Content.ReadAsStringAsync(
                cancellationToken);
            Assert.True(
                firstResponse.StatusCode == HttpStatusCode.OK,
                $"Completarea a răspuns {(int)firstResponse.StatusCode}: {firstBody}");

            using var retryRequest = CreateSignedRequest(
                HttpMethod.Post,
                $"/api/integration/registry-entries/{entryId}/documents",
                body,
                key);
            using var retryResponse = await client.SendAsync(
                retryRequest,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
            Assert.Equal(firstBody, await retryResponse.Content.ReadAsStringAsync(
                cancellationToken));

            await using var verificationScope = factory.Services
                .CreateAsyncScope();
            var verificationDb = verificationScope.ServiceProvider
                .GetRequiredService<DmsDbContext>();
            Assert.Equal(EntryStatus.InReview, (await verificationDb
                .RegistryEntries.SingleAsync(item => item.id == entryId,
                    cancellationToken)).status);
            Assert.Equal(1, await verificationDb.RegistryDocuments.CountAsync(
                cancellationToken));
            Assert.Equal(1, await verificationDb.InboundRequests.CountAsync(item =>
                item.endpoint ==
                    "/api/integration/registry-entries/{entryId}/documents" &&
                item.idempotency_key == key,
                cancellationToken));
            Assert.Single(await verificationDb.OutboxMessages
                .Where(item => item.aggregate_id == entryId)
                .ToListAsync(cancellationToken));
        }

        [Fact]
        public async Task Cancellation_is_idempotent_and_emits_status_callback()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var externalId = Guid.NewGuid();
            var entryId = Guid.NewGuid();

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
                var actor = await GetOrCreateIntegrationActorAsync(
                    db,
                    cancellationToken);
                var entry = new RegistryEntry
                {
                    id = entryId,
                    external_id = externalId,
                    registry_type_id = Guid.NewGuid(),
                    year = 2026,
                    number = 1,
                    direction = EntryDirection.In,
                    registered_at = DateTimeOffset.UtcNow,
                    subject = "Retragere test",
                    applicant_name = "Cetățean Test",
                    deadline = DateOnly.FromDateTime(DateTime.UtcNow),
                    status = EntryStatus.Registered,
                    created_by_user_id = actor.id
                };
                db.RegistryEntries.Add(entry);
                await db.SaveChangesAsync(cancellationToken);
            }

            var body = JsonSerializer.Serialize(new
            {
                externalId,
                entryId,
                reason = "Nu mai este necesară."
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var key = Guid.NewGuid().ToString();
            using var client = factory.CreateClient();

            using var firstRequest = CreateSignedRequest(
                HttpMethod.Post,
                $"/api/integration/registry-entries/{entryId}/cancel",
                body,
                key);
            using var firstResponse = await client.SendAsync(
                firstRequest,
                cancellationToken);
            var firstBody = await firstResponse.Content.ReadAsStringAsync(
                cancellationToken);
            Assert.True(firstResponse.StatusCode == HttpStatusCode.OK,
                $"Retragerea a răspuns {(int)firstResponse.StatusCode}: {firstBody}");

            using var retryRequest = CreateSignedRequest(
                HttpMethod.Post,
                $"/api/integration/registry-entries/{entryId}/cancel",
                body,
                key);
            using var retryResponse = await client.SendAsync(
                retryRequest,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
            Assert.Equal(firstBody, await retryResponse.Content.ReadAsStringAsync(
                cancellationToken));

            await using var verificationScope = factory.Services.CreateAsyncScope();
            var verificationDb = verificationScope.ServiceProvider
                .GetRequiredService<DmsDbContext>();
            var entryAfterCancellation = await verificationDb.RegistryEntries
                .SingleAsync(item => item.id == entryId, cancellationToken);
            Assert.Equal(EntryStatus.Cancelled, entryAfterCancellation.status);
            Assert.Equal("Nu mai este necesară.", entryAfterCancellation.status_note);
            Assert.Equal(1, await verificationDb.InboundRequests.CountAsync(item =>
                item.endpoint ==
                    "/api/integration/registry-entries/{entryId}/cancel" &&
                item.idempotency_key == key,
                cancellationToken));
            Assert.Single(await verificationDb.OutboxMessages
                .Where(item => item.aggregate_id == entryId &&
                    item.event_type == "RegistryEntry.StatusChanged")
                .ToListAsync(cancellationToken));
        }

        private static HttpRequestMessage CreateSignedRequest(
            HttpMethod method,
            string path,
            string body = "",
            string? idempotencyKey = null)
        {
            var timestamp = DateTimeOffset.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(
                DmsApiFactory.PortalIntegrationSecret));
            var signature = Convert.ToHexString(hmac.ComputeHash(
                Encoding.UTF8.GetBytes($"{timestamp}.{body}")))
                .ToLowerInvariant();

            var request = new HttpRequestMessage(method, path);

            if (method != HttpMethod.Get)
            {
                request.Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json");
            }

            if (idempotencyKey is not null)
            {
                request.Headers.Add("Idempotency-Key", idempotencyKey);
            }

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                DmsApiFactory.PortalIntegrationSecret);
            request.Headers.Add("X-Timestamp", timestamp);
            request.Headers.Add("X-Signature", $"sha256={signature}");

            return request;
        }

        private static async Task<User> GetOrCreateIntegrationActorAsync(
            DmsDbContext db,
            CancellationToken cancellationToken)
        {
            var actor = await db.Users.SingleOrDefaultAsync(user =>
                user.email == "integration@example.com",
                cancellationToken);
            if (actor is not null)
            {
                return actor;
            }

            actor = new User
            {
                id = Guid.NewGuid(),
                email = "integration@example.com",
                full_name = "Integrare Portal",
                role = Role.Admin,
                password_hash = "unused",
                email_confirmed = true,
                is_active = true
            };
            db.Users.Add(actor);
            return actor;
        }
    }
}
