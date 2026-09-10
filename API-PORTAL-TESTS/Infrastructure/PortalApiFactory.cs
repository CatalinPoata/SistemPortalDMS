using API_PORTAL.Auth;
using API_PORTAL.Data;
using API_PORTAL.Email;
using API_PORTAL.Integration;
using API_PORTAL.Storage;
using API_PORTAL_TESTS.Data;
using API_PORTAL_TESTS.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Reporting;


namespace API_PORTAL_TESTS.Infrastructure
{
    public sealed class PortalApiFactory
    : WebApplicationFactory<Program>
    {
        public const string DmsIntegrationSecret =
            "test-portal-dms-shared-secret-at-least-32-bytes";

        private readonly Dictionary<string, string?> previousValues = [];
        private readonly string databaseName =
            $"portal-http-tests-{Guid.NewGuid():N}";

        private readonly string? postgresConnectionString;

        public string StorageRootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            $"portal-http-files-{Guid.NewGuid():N}");

        public FakeAccountEmailService EmailService { get; } = new();

        public FakeDmsRegistryTypeClient DmsRegistryTypes { get; } = new();

        public FakeDmsSubmissionRegistrationClient DmsRegistrations { get; }
            = new();

        public FakeDmsRegistrationReceiptClient DmsReceipts { get; } = new();

        public FakePdfRenderer PdfRenderer { get; } = new();

        public PortalApiFactory()
            : this(null)
        {
        }

        internal PortalApiFactory(string? postgresConnectionString)
        {
            if (postgresConnectionString is not null)
            {
                var testDatabase = new NpgsqlConnectionStringBuilder(
                    postgresConnectionString).Database;

                if (testDatabase?.EndsWith(
                        "_test",
                        StringComparison.OrdinalIgnoreCase) != true)
                {
                    throw new InvalidOperationException(
                        "Testele PostgreSQL necesită o bază dedicată, " +
                        "cu numele terminat în '_test'.");
                }
            }

            this.postgresConnectionString = postgresConnectionString;
            SetEnvironmentVariable(
                "ConnectionStrings__PortalDatabase",
                "Host=127.0.0.1;Port=5432;" +
                "Database=unused;" +
                "Username=unused;" +
                "Password=unused");
            SetEnvironmentVariable("Jwt__Issuer", "TestPortalIssuer");
            SetEnvironmentVariable("Jwt__Audience", "TestPortalAudience");
            SetEnvironmentVariable(
                "Jwt__SigningKey",
                "test-portal-signing-key-with-at-least-32-bytes");
            SetEnvironmentVariable(
                "FileDownload__SigningKey",
                "test-portal-file-download-key-with-at-least-32-bytes");
            SetEnvironmentVariable(
                "Integration__Dms__BaseUrl",
                "https://dms.test/");
            SetEnvironmentVariable(
                "Integration__Dms__SharedSecret",
                DmsIntegrationSecret);
        }

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddDataProtection()
                    .UseEphemeralDataProtectionProvider();

                ConfigureDatabase(services);

                services.PostConfigure<SubmissionFileStorageOptions>(
                    options => options.RootPath = StorageRootPath);

                services.RemoveAll<IDmsRegistryTypeClient>();
                services.AddSingleton<IDmsRegistryTypeClient>(
                    DmsRegistryTypes);

                services.RemoveAll<IDmsSubmissionRegistrationClient>();
                services.AddSingleton<IDmsSubmissionRegistrationClient>(
                    DmsRegistrations);

                services.RemoveAll<IDmsRegistrationReceiptClient>();
                services.AddSingleton<IDmsRegistrationReceiptClient>(DmsReceipts);

                services.RemoveAll<IAccountEmailService>();
                services.AddSingleton<IAccountEmailService>(EmailService);

                services.RemoveAll<IPdfRenderer>();
                services.AddSingleton<IPdfRenderer>(PdfRenderer);
            });
        }

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                foreach (var item in previousValues)
                {
                    Environment.SetEnvironmentVariable(item.Key, item.Value);
                }

                if (Directory.Exists(StorageRootPath))
                {
                    Directory.Delete(StorageRootPath, recursive: true);
                }
            }

            base.Dispose(disposing);
        }

        private void SetEnvironmentVariable(string name, string value)
        {
            previousValues[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        private void ConfigureDatabase(IServiceCollection services)
        {
            services.RemoveAll<
                IDbContextOptionsConfiguration<PortalDbContext>>();

            services.RemoveAll<DbContextOptions<PortalDbContext>>();
            services.RemoveAll<PortalDbContext>();
            services.RemoveAll<IRefreshSessionLock>();

            if (postgresConnectionString is not null)
            {
                services.AddDbContext<PortalDbContext>(options =>
                    options.UseNpgsql(
                        postgresConnectionString,
                        npgsql => npgsql.MigrationsHistoryTable(
                            "__EFMigrationsHistory",
                            "portal")));

                services.AddScoped<
                    IRefreshSessionLock,
                    PostgresRefreshSessionLock>();

                return;
            }

            services.AddDbContext<PortalDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            services.AddScoped<PortalDbContext>(provider =>
                new TestPortalDbContext(
                    provider.GetRequiredService<
                        DbContextOptions<PortalDbContext>>()));

            services.AddSingleton<
                IRefreshSessionLock,
                NoopRefreshSessionLock>();
        }
    }
}
