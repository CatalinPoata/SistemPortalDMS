using API_DMS.Entities;
using API_DMS.Services;
using API_DMS_TESTS.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;
using Task = System.Threading.Tasks.Task;
using Microsoft.EntityFrameworkCore;

namespace API_DMS_TESTS.Tests
{
    public sealed class RegistryNumberAllocatorTests
    {
        [Fact]
        public async Task T1_Fifty_parallel_allocations_are_consecutive()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var connectionString =
                Environment.GetEnvironmentVariable(
                    "DMS_TEST_CONNECTION");

            if (string.IsNullOrWhiteSpace(
                    connectionString))
            {
                throw SkipException.ForSkip(
                    "Variabila DMS_TEST_CONNECTION nu este configurată.");
            }

            var registryId = Guid.NewGuid();

            await using var setup =
                TestDbContextFactory.CreatePostgres(
                    connectionString);

            var registry = new RegistryType
            {
                id = registryId,
                code = $"T{Guid.NewGuid():N}"
                    .Substring(0, 20)
                    .ToUpperInvariant(),
                name = "Registry test automat",
                direction = RegistryDirection.In,
                start_number = 1,
                default_deadline_days = 30,
                is_closed = false
            };

            setup.RegistryTypes.Add(registry);

            await setup.SaveChangesAsync(cancellationToken);

            try
            {
                var numbers =
                    await Task.WhenAll(
                        Enumerable.Range(0, 50)
                            .Select(_ =>
                                AllocateAsync(
                                    connectionString,
                                    registryId)));

                Assert.Equal(
                    50,
                    numbers.Distinct().Count());

                Assert.Equal(
                    Enumerable.Range(1, 50)
                        .Select(number =>
                            (long)number),
                    numbers.OrderBy(number => number));

                await using var verification =
                    TestDbContextFactory.CreatePostgres(
                        connectionString);

                var counter =
                    await verification
                        .RegistryNumberCounters
                        .SingleAsync(
                            counter =>
                                counter.registry_type_id == registryId &&
                                counter.year == DateTime.UtcNow.Year,
                            cancellationToken);

                Assert.Equal(50, counter.last_number);
            }
            finally
            {
                await using var cleanup =
                    TestDbContextFactory.CreatePostgres(
                        connectionString);

                var counters =
                    await cleanup.RegistryNumberCounters
                        .Where(counter =>
                            counter.registry_type_id ==
                                registryId)
                        .ToListAsync(cancellationToken);

                cleanup.RegistryNumberCounters
                    .RemoveRange(counters);

                var registryToDelete =
                    await cleanup
                        .RegistryTypes
                        .SingleOrDefaultAsync(
                            registry =>
                                registry.id == registryId,
                            cancellationToken);

                if (registryToDelete is not null)
                {
                    cleanup.RegistryTypes
                        .Remove(registryToDelete);
                }

                await cleanup.SaveChangesAsync(cancellationToken);
            }
        }

        private static async Task<long> AllocateAsync(
            string connectionString,
            Guid registryId)
        {
            await using var db =
                TestDbContextFactory.CreatePostgres(
                    connectionString);

            var allocator =
                new RegistryNumberAllocator(db);

            return await allocator.AllocateAsync(
                registryId,
                DateTime.UtcNow.Year);
        }
    }
}
