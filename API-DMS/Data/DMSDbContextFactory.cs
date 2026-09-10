using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace API_DMS.Data
{
    public class DmsDbContextFactory
        : IDesignTimeDbContextFactory<DmsDbContext>
    {
        public DmsDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(
                    "appsettings.json",
                    optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString =
                configuration.GetConnectionString("DmsDatabase")
                ?? throw new InvalidOperationException(
                    "Connection string 'DmsDatabase' was not found.");

            var dataSource =
                DmsDataSourceFactory.Create(connectionString);

            var options =
                new DbContextOptionsBuilder<DmsDbContext>();

            options.UseNpgsql(
                dataSource,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable(
                        "__EFMigrationsHistory",
                        "dms");
                });

            return new DmsDbContext(options.Options);
        }
    }
}
