using API_PORTAL.Auth;
using API_PORTAL.Data;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL.Integration;
using API_PORTAL_TESTS.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace API_PORTAL_TESTS.Tests
{
    public sealed class RegistrationReceiptHttpTests
        : IClassFixture<PortalApiFactory>
    {
        private readonly PortalApiFactory factory;

        public RegistrationReceiptHttpTests(PortalApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task P16_Owner_can_download_registration_receipt_as_pdf()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            factory.DmsReceipts.Reset();
            var entryId = Guid.NewGuid();
            factory.DmsReceipts.Result = new DmsReceiptDownloadResult(
                DmsReceiptDownloadStatus.Found,
                Encoding.ASCII.GetBytes("%PDF-1.7\nreceipt"),
                null);

            var owner = await SeedUserAsync(cancellationToken);
            var submissionId = await SeedRegisteredSubmissionAsync(
                owner,
                entryId,
                cancellationToken);
            using var client = await CreateClientAsync(owner, cancellationToken);

            using var response = await client.GetAsync(
                $"/api/submissions/{submissionId}/registration-receipt",
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("dovada-inregistrare-1-2026.pdf",
                response.Content.Headers.ContentDisposition?.ToString() ?? string.Empty);
            Assert.Equal(entryId, factory.DmsReceipts.LastEntryId);
            Assert.Equal(1, factory.DmsReceipts.DownloadCount);
            Assert.Equal("%PDF-1.7\nreceipt", await response.Content.ReadAsStringAsync(cancellationToken));
        }

        [Fact]
        public async Task P16_Other_citizen_cannot_request_registration_receipt()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            factory.DmsReceipts.Reset();
            var owner = await SeedUserAsync(cancellationToken);
            var submissionId = await SeedRegisteredSubmissionAsync(
                owner,
                Guid.NewGuid(),
                cancellationToken);
            var other = await SeedUserAsync(cancellationToken);
            using var client = await CreateClientAsync(other, cancellationToken);

            using var response = await client.GetAsync(
                $"/api/submissions/{submissionId}/registration-receipt",
                cancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(0, factory.DmsReceipts.DownloadCount);
        }

        private async Task<User> SeedUserAsync(CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var user = new User
            {
                id = Guid.NewGuid(),
                email = $"receipt-{Guid.NewGuid():N}@example.com",
                full_name = "Cetățean dovadă",
                password_hash = "unused",
                role = Role.Citizen,
                email_confirmed = true,
                is_active = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return user;
        }

        private async Task<Guid> SeedRegisteredSubmissionAsync(
            User user,
            Guid entryId,
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var service = new ServiceDefinition
            {
                id = Guid.NewGuid(),
                code = $"receipt-{Guid.NewGuid():N}",
                title = "Serviciu dovadă",
                registry_type_code = "INTRARI",
                form_schema = System.Text.Json.JsonDocument.Parse("{\"sections\":[]}"),
                is_published = true
            };
            var submission = new Submission
            {
                id = Guid.NewGuid(),
                external_id = Guid.NewGuid(),
                service_id = service.id,
                schema_version = 1,
                form_snapshot = System.Text.Json.JsonDocument.Parse("{\"sections\":[]}"),
                user_id = user.id,
                values = System.Text.Json.JsonDocument.Parse("{}"),
                status = SubmissionStatus.Registered,
                submitted_at = DateTimeOffset.UtcNow,
                registered_at = DateTimeOffset.UtcNow,
                dms_entry_id = entryId,
                registry_number = 1,
                registry_year = 2026,
                registry_display_number = "1/2026"
            };
            db.AddRange(service, submission);
            await db.SaveChangesAsync(cancellationToken);
            return submission.id;
        }

        private async Task<HttpClient> CreateClientAsync(
            User user,
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false
            });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                tokens.Create(user).AccessToken);
            return client;
        }
    }
}
