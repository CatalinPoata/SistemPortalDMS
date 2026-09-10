using API_DMS.Auth;
using API_DMS.Data;
using API_DMS.Email;
using API_DMS.Integration;
using API_DMS.Storage;
using API_DMS_TESTS.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace API_DMS_TESTS.Infrastructure
{
    public sealed class DmsApiFactory
    : WebApplicationFactory<Program>
    {
        public const string PortalIntegrationSecret =
            "test-portal-dms-shared-secret-at-least-32-bytes";

        private readonly Dictionary<string, string?> previousValues = [];
        private readonly string databaseName = $"dms-http-tests-{Guid.NewGuid():N}";
        private readonly string? postgresConnectionString;
        private readonly IInterceptor? sqlInterceptor;

        public string StorageRootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            $"dms-http-files-{Guid.NewGuid():N}");

        public FakeAccountEmailService EmailService { get; } = new();

        public FakePortalSubmissionFileClient PortalFiles { get; } = new();

        public DmsApiFactory()
            : this(null)
        {
        }

        internal DmsApiFactory(
            string? postgresConnectionString,
            IInterceptor? sqlInterceptor = null)
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
            this.sqlInterceptor = sqlInterceptor;
            SetEnvironmentVariable(
                "ConnectionStrings__DmsDatabase",
                "Host=127.0.0.1;Port=5432;" +
                "Database=unused;" +
                "Username=unused;" +
                "Password=unused");
            SetEnvironmentVariable("Jwt__Issuer", "TestIssuer");
            SetEnvironmentVariable("Jwt__Audience", "TestAudience");
            SetEnvironmentVariable(
                "Jwt__SigningKey",
                "test-signing-key-with-at-least-32-bytes");
            SetEnvironmentVariable(
                "FileDownload__SigningKey",
                "test-download-key-with-at-least-32-bytes");
            SetEnvironmentVariable("FileDownload__LifetimeMinutes", "15");
            SetEnvironmentVariable(
                "Integration__Portal__SharedSecret",
                PortalIntegrationSecret);
            SetEnvironmentVariable(
                "Integration__Portal__BaseUrl",
                "https://portal.test/");
            SetEnvironmentVariable(
                "Integration__Portal__ActorEmail",
                "integration@example.com");
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

                services.PostConfigure<FileStorageOptions>(
                    options => options.RootPath = StorageRootPath);

                services.RemoveAll<IAccountEmailService>();
                services.AddSingleton<IAccountEmailService>(EmailService);

                services.RemoveAll<IPortalSubmissionFileClient>();
                services.AddSingleton<IPortalSubmissionFileClient>(PortalFiles);
            });
        }

        protected override void Dispose(bool disposing)
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
                IDbContextOptionsConfiguration<DmsDbContext>>();

            services.RemoveAll<DbContextOptions<DmsDbContext>>();
            services.RemoveAll<DmsDbContext>();
            services.RemoveAll<IRefreshSessionLock>();

            if (postgresConnectionString is not null)
            {
                services.AddDbContext<DmsDbContext>(options =>
                {
                    options.UseNpgsql(
                        postgresConnectionString,
                        npgsql => npgsql.MigrationsHistoryTable(
                            "__EFMigrationsHistory",
                            "dms"));

                    if (sqlInterceptor is not null)
                    {
                        options.AddInterceptors(sqlInterceptor);
                    }
                });

                services.AddScoped<
                    IRefreshSessionLock,
                    PostgresRefreshSessionLock>();

                return;
            }

            services.AddDbContext<DmsDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            services.AddScoped<DmsDbContext>(provider =>
                new TestDmsDbContext(
                    provider.GetRequiredService<
                        DbContextOptions<DmsDbContext>>()));

            services.AddSingleton<
                IRefreshSessionLock,
                NoopRefreshSessionLock>();
        }
    }
}
