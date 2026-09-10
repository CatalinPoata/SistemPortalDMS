using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace API_PORTAL.Data
{
    public class PortalDbContextFactory
        : IDesignTimeDbContextFactory<PortalDbContext>
    {
        public PortalDbContext CreateDbContext(string[] args)
        {
            var configuration =
                new ConfigurationBuilder()
                    .SetBasePath(
                        Directory.GetCurrentDirectory())
                    .AddJsonFile(
                        "appsettings.json",
                        optional: true)
                    .AddEnvironmentVariables()
                    .Build();

            var connectionString =
                configuration.GetConnectionString("PortalDatabase")
                ?? throw new InvalidOperationException(
                    "Connection string 'PortalDatabase' was not found.");

            var dataSource =
                PortalDataSourceFactory.Create(connectionString);

            var options =
                new DbContextOptionsBuilder<PortalDbContext>();

            options.UseNpgsql(
                dataSource,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable(
                        "__EFMigrationsHistory",
                        "portal");
                });

            return new PortalDbContext(options.Options);
        }
    }
}
