using API_PORTAL.Data;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL_TESTS.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace API_PORTAL_TESTS.Tests
{
    public sealed class RegistryEventsCallbackHttpTests
        : IClassFixture<PortalApiFactory>
    {
        private readonly PortalApiFactory factory;

        public RegistryEventsCallbackHttpTests(PortalApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task T4_Registry_callback_is_deduplicated_and_updates_submission()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var externalId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var responseDocumentId = Guid.NewGuid();
            var submissionId = await SeedSubmissionAsync(
                externalId,
                cancellationToken);
            var eventId = Guid.NewGuid();
            var body = JsonSerializer.Serialize(new
            {
                eventId,
                entryId,
                externalId,
                status = "Completed",
                message = "Cererea a fost soluționată.",
                occurredAt = DateTimeOffset.UtcNow,
                responseDocument = new
                {
                    documentId = responseDocumentId,
                    name = "raspuns.pdf",
                    contentType = "application/pdf",
                    sizeBytes = 42,
                    sha256 = new string('a', 64)
                }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            using var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false
                });

            using var firstRequest = CreateSignedRequest(body);
            using var firstResponse = await client.SendAsync(
                firstRequest,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

            using var retryRequest = CreateSignedRequest(body);
            using var retryResponse = await client.SendAsync(
                retryRequest,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var submission = await db.Submissions.SingleAsync(
                item => item.id == submissionId,
                cancellationToken);
            Assert.Equal(SubmissionStatus.Completed, submission.status);
            Assert.Equal(entryId, submission.dms_entry_id);
            Assert.Equal(1, await db.InboxEvents.CountAsync(item =>
                item.event_id == eventId,
                cancellationToken));
            Assert.Equal(1, await db.Notifications.CountAsync(item =>
                item.user_id == submission.user_id &&
                item.link_url == $"/cereri/{submissionId}",
                cancellationToken));

            var file = await db.SubmissionFiles.SingleAsync(
                item => item.id == responseDocumentId,
                cancellationToken);
            Assert.Equal(responseDocumentId, file.id);
            Assert.Equal(SubmissionFileKind.Response, file.kind);
            Assert.Equal("dms/" + responseDocumentId.ToString("N"),
                file.storage_key);

            Assert.Equal(1, await db.SubmissionEvents.CountAsync(item =>
                item.submission_id == submissionId,
                cancellationToken));
        }

        [Fact]
        public async Task Info_requested_callback_persists_the_dms_reason()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var externalId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            var submissionId = await SeedSubmissionAsync(
                externalId,
                cancellationToken);
            var body = JsonSerializer.Serialize(new
            {
                eventId = Guid.NewGuid(),
                entryId,
                externalId,
                status = "InfoRequested",
                message = "Au fost solicitate clarificări.",
                statusNote = "Atașează copia actului de identitate.",
                occurredAt = DateTimeOffset.UtcNow
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            using var client = factory.CreateClient();
            using var request = CreateSignedRequest(body);
            using var response = await client.SendAsync(
                request,
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var submission = await db.Submissions.SingleAsync(
                item => item.id == submissionId,
                cancellationToken);
            var notification = await db.Notifications.SingleAsync(
                item => item.user_id == submission.user_id,
                cancellationToken);
            var submissionEvent = await db.SubmissionEvents.SingleAsync(
                item => item.submission_id == submissionId,
                cancellationToken);

            Assert.Equal(SubmissionStatus.InfoRequested, submission.status);
            Assert.Equal(
                "Atașează copia actului de identitate.",
                submission.status_details);
            Assert.Contains("Atașează copia actului", notification.body);
            Assert.Contains("Atașează copia actului", submissionEvent.message);
        }

        private async Task<Guid> SeedSubmissionAsync(
            Guid externalId,
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var user = new User
            {
                id = Guid.NewGuid(),
                email = $"callback-{Guid.NewGuid():N}@example.com",
                full_name = "Cetățean Callback",
                role = Role.Citizen,
                password_hash = "unused",
                email_confirmed = true,
                is_active = true
            };
            var service = new ServiceDefinition
            {
                id = Guid.NewGuid(),
                code = $"callback-{Guid.NewGuid():N}",
                title = "Serviciu callback",
                form_schema = JsonDocument.Parse("{\"sections\":[]}"),
                schema_version = 1,
                registry_type_code = "INTRARI",
                is_published = true
            };
            var submission = new Submission
            {
                id = Guid.NewGuid(),
                external_id = externalId,
                service_id = service.id,
                schema_version = 1,
                form_snapshot = JsonDocument.Parse("{\"sections\":[]}"),
                user_id = user.id,
                values = JsonDocument.Parse("{}"),
                status = SubmissionStatus.Registered,
                submitted_at = DateTimeOffset.UtcNow
            };

            db.AddRange(user, service, submission);
            await db.SaveChangesAsync(cancellationToken);
            return submission.id;
        }

        private static HttpRequestMessage CreateSignedRequest(string body)
        {
            var timestamp = DateTimeOffset.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(
                PortalApiFactory.DmsIntegrationSecret));
            var signature = Convert.ToHexString(hmac.ComputeHash(
                Encoding.UTF8.GetBytes($"{timestamp}.{body}")))
                .ToLowerInvariant();

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/callbacks/registry-events")
            {
                Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                PortalApiFactory.DmsIntegrationSecret);
            request.Headers.Add("X-Timestamp", timestamp);
            request.Headers.Add("X-Signature", $"sha256={signature}");

            return request;
        }
    }
}
