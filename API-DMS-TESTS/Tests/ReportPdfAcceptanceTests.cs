using API_DMS.Auth;
using API_DMS.Data;
using API_DMS.Entities;
using API_DMS.Entities.Base;
using API_DMS.Seed;
using API_DMS_TESTS.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace API_DMS_TESTS.Tests
{
    public sealed class ReportPdfAcceptanceTests : IClassFixture<DmsApiFactory>
    {
        private readonly DmsApiFactory factory;

        public ReportPdfAcceptanceTests(DmsApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task T15_Fixed_registry_report_has_one_page_and_romanian_diacritics()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var registryId = await SeedFixedEntriesAsync(cancellationToken);
            using var client = await CreateAdminClientAsync(cancellationToken);
            using var response = await client.PostAsync(
                "/api/report-definitions/registru-intrari-iesiri/export?" +
                $"registryTypeId={registryId}&" +
                "dateFrom=2026-09-01&dateTo=2026-09-30",
                content: null,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Assert.Fail(
                    $"Exportul T15 a răspuns {(int)response.StatusCode}: " +
                    await response.Content.ReadAsStringAsync(cancellationToken));
            }

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

            var bytes = await response.Content.ReadAsByteArrayAsync(
                cancellationToken);
            Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));

            using var document = PdfDocument.Open(bytes);
            Assert.Equal(1, document.NumberOfPages);

            var text = string.Join(
                "\n",
                document.GetPages().Select(page => page.Text));
            var normalized = text.Normalize(NormalizationForm.FormC);

            Assert.Contains("Registru intrări-ieșiri", normalized);
            Assert.Contains("Ștefan Țăranu", normalized);
            Assert.Contains("Cerere cu diacritice: Știință și Țară", normalized);
            Assert.Matches(new Regex(@"101(?=10\.09\.2026)"), normalized);
            Assert.Matches(new Regex(@"202(?=10\.09\.2026)"), normalized);
        }

        private async Task<Guid> SeedFixedEntriesAsync(
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await DmsDbSeeder.SeedAsync(scope.ServiceProvider, cancellationToken);

            var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
            var admin = await db.Users.SingleAsync(
                user => user.email == "admin@example.com",
                cancellationToken);
            var registry = new RegistryType
            {
                id = Guid.NewGuid(),
                code = $"T15{Guid.NewGuid():N}"[..30].ToUpperInvariant(),
                name = "Registru T15 Știință",
                direction = RegistryDirection.Both,
                start_number = 1,
                default_deadline_days = 30,
                is_closed = false
            };
            var registeredAt = new DateTimeOffset(
                2026, 9, 10, 9, 30, 0, TimeSpan.Zero);

            db.RegistryTypes.Add(registry);
            db.RegistryEntries.AddRange(
                CreateEntry(registry, admin, 101, registeredAt),
                CreateEntry(registry, admin, 202, registeredAt.AddMinutes(5)));
            await db.SaveChangesAsync(cancellationToken);

            return registry.id;
        }

        private static RegistryEntry CreateEntry(
            RegistryType registry,
            User admin,
            long number,
            DateTimeOffset registeredAt)
        {
            return new RegistryEntry
            {
                id = Guid.NewGuid(),
                registry_type_id = registry.id,
                year = 2026,
                number = number,
                direction = EntryDirection.In,
                registered_at = registeredAt,
                subject = "Cerere cu diacritice: Știință și Țară",
                applicant_name = "Ștefan Țăranu",
                applicant_email = "stefan@example.com",
                deadline = new DateOnly(2026, 10, 10),
                status = EntryStatus.Registered,
                created_by_user_id = admin.id
            };
        }

        private async Task<HttpClient> CreateAdminClientAsync(
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
            var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var admin = await db.Users.SingleAsync(
                user => user.email == "admin@example.com",
                cancellationToken);
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false
            });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                tokens.Create(admin).AccessToken);
            return client;
        }
    }
}
