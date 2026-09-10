using API_DMS.Data;
using API_DMS.Entities;
using API_DMS.Reports;
using API_DMS.Seed;
using API_DMS_TESTS.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Shared.Reporting;
using Task = System.Threading.Tasks.Task;
using Xunit;

namespace API_DMS_TESTS.Tests
{
    public sealed class ReportSeedTests
        : IClassFixture<DmsApiFactory>
    {
        private readonly DmsApiFactory factory;

        public ReportSeedTests(DmsApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task System_reports_are_seeded_idempotently_and_are_valid()
        {
            var cancellationToken =
                TestContext.Current.CancellationToken;

            await using (var seedScope =
                factory.Services.CreateAsyncScope())
            {
                await DmsDbSeeder.SeedAsync(
                    seedScope.ServiceProvider,
                    cancellationToken);
            }

            await using (var seedScope =
                factory.Services.CreateAsyncScope())
            {
                await DmsDbSeeder.SeedAsync(
                    seedScope.ServiceProvider,
                    cancellationToken);
            }

            await using var verificationScope =
                factory.Services.CreateAsyncScope();

            var db = verificationScope.ServiceProvider
                .GetRequiredService<DmsDbContext>();

            var validator = verificationScope.ServiceProvider
                .GetRequiredService<DmsReportDefinitionValidator>();

            var reports = await db.ReportDefinitions
                .AsNoTracking()
                .Where(report =>
                    report.code == "registru-intrari-iesiri" ||
                    report.code == "dovada-inregistrare")
                .OrderBy(report => report.code)
                .ToListAsync(cancellationToken);

            Assert.Equal(2, reports.Count);
            Assert.All(reports, report => Assert.True(report.is_system));

            foreach (var report in reports)
            {
                var errors = validator.Validate(
                    report.dataset_key,
                    report.definition.RootElement);

                Assert.Empty(errors);
            }

            var evidenceReport = reports.Single(report =>
                report.code == "dovada-inregistrare");

            var evidenceDefinition = ReportJson.Parse(
                evidenceReport.definition.RootElement);

            var entryId = Assert.Single(
                evidenceDefinition.Parameters);

            Assert.Equal("entryId", entryId.Name);
            Assert.Equal("string", entryId.Type);
            Assert.True(entryId.Required);
        }

        [Fact]
        public async Task Evidence_report_returns_only_the_requested_entry()
        {
            var cancellationToken =
                TestContext.Current.CancellationToken;

            await using var scope =
                factory.Services.CreateAsyncScope();

            await DmsDbSeeder.SeedAsync(
                scope.ServiceProvider,
                cancellationToken);

            var db = scope.ServiceProvider
                .GetRequiredService<DmsDbContext>();

            var admin = await db.Users.SingleAsync(
                user => user.email == "admin@example.com",
                cancellationToken);

            var registryType = new RegistryType
            {
                id = Guid.NewGuid(),
                code = "RAPORT-TEST",
                name = "Registru test rapoarte",
                direction = RegistryDirection.Both,
                start_number = 1,
                default_deadline_days = 30,
                is_closed = false
            };

            var requestedEntry = new RegistryEntry
            {
                id = Guid.NewGuid(),
                registry_type_id = registryType.id,
                year = 2026,
                number = 7,
                direction = EntryDirection.In,
                registered_at = new DateTimeOffset(
                    2026,
                    9,
                    6,
                    10,
                    0,
                    0,
                    TimeSpan.Zero),
                deadline = new DateOnly(2026, 10, 6),
                subject = "Adeverință pentru Ștefan",
                applicant_name = "Ionescu Ștefan",
                status = EntryStatus.Registered,
                created_by_user_id = admin.id
            };

            var otherEntry = new RegistryEntry
            {
                id = Guid.NewGuid(),
                registry_type_id = registryType.id,
                year = 2026,
                number = 8,
                direction = EntryDirection.In,
                registered_at = new DateTimeOffset(
                    2026,
                    9,
                    6,
                    11,
                    0,
                    0,
                    TimeSpan.Zero),
                deadline = new DateOnly(2026, 10, 6),
                subject = "Altă poziție",
                applicant_name = "Popescu Ana",
                status = EntryStatus.Registered,
                created_by_user_id = admin.id
            };

            db.AddRange(registryType, requestedEntry, otherEntry);
            await db.SaveChangesAsync(cancellationToken);

            var report = await db.ReportDefinitions
                .AsNoTracking()
                .SingleAsync(
                    item => item.code == "dovada-inregistrare",
                    cancellationToken);

            var previewService = scope.ServiceProvider
                .GetRequiredService<DmsReportPreviewService>();

            var query = new QueryCollection(
                new Dictionary<string, StringValues>
                {
                    ["entryId"] = requestedEntry.id.ToString()
                });

            var preview = await previewService.PreviewAsync(
                report,
                query,
                cancellationToken);

            var row = Assert.Single(preview.Rows);

            Assert.Equal("7/2026", row["display_number"]);
            Assert.Equal(
                "Adeverință pentru Ștefan",
                row["subject"]);

            var invalidQuery = new QueryCollection(
                new Dictionary<string, StringValues>
                {
                    ["entryId"] = "nu-este-un-uuid"
                });

            var exception = await Assert.ThrowsAsync<
                ReportPreviewValidationException>(() =>
                previewService.PreviewAsync(
                    report,
                    invalidQuery,
                    cancellationToken));

            Assert.Contains("entryId", exception.Errors.Keys);
        }
    }
}
