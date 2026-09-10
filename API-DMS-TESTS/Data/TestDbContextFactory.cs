using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using API_DMS.Data;

namespace API_DMS_TESTS.Data
{
    internal static class TestDbContextFactory
    {
        public static DmsDbContext CreateInMemory()
        {
            var options =
                new DbContextOptionsBuilder<DmsDbContext>()
                    .UseInMemoryDatabase(
                        Guid.NewGuid().ToString())
                    .Options;

            return new TestDmsDbContext(options);
        }

        public static DmsDbContext CreatePostgres(
            string connectionString)
        {
            var options =
                new DbContextOptionsBuilder<DmsDbContext>()
                    .UseNpgsql(
                        connectionString,
                        npgsql =>
                        {
                            npgsql.MigrationsHistoryTable(
                                "__EFMigrationsHistory",
                                "dms");
                        })
                    .Options;

            return new DmsDbContext(options);
        }
    }
}
