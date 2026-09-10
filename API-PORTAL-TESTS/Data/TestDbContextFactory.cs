using API_PORTAL.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace API_PORTAL_TESTS.Data
{
    internal static class TestDbContextFactory
    {
        public static PortalDbContext CreateInMemory()
        {
            var options =
                new DbContextOptionsBuilder<PortalDbContext>()
                    .UseInMemoryDatabase(
                        Guid.NewGuid().ToString())
                    .Options;

            return new TestPortalDbContext(options);
        }

        public static PortalDbContext CreatePostgres(
            string connectionString)
        {
            var options =
                new DbContextOptionsBuilder<PortalDbContext>()
                    .UseNpgsql(
                        connectionString,
                        npgsql =>
                        {
                            npgsql.MigrationsHistoryTable(
                                "__EFMigrationsHistory",
                                "portal");
                        })
                    .Options;

            return new PortalDbContext(options);
        }
    }
}
