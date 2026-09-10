using API_DMS.Data;
using RegistryNumberCounter = API_DMS.Entities.RegistryNumberCounter;
using Microsoft.EntityFrameworkCore;

namespace API_DMS.Services
{
    public sealed class RegistryNumberAllocator
    {
        private readonly DmsDbContext db;

        public RegistryNumberAllocator(DmsDbContext db)
        {
            this.db = db;
        }

        public Task<long> AllocateAsync(
            Guid registryTypeId,
            int year)
        {
            if (!db.Database.IsRelational())
            {
                return AllocateForNonRelationalTestsAsync(
                    registryTypeId,
                    year);
            }

            var number = db.Database
                .SqlQuery<long>($"""
                    INSERT INTO dms.registry_number_counter
                        (registry_type_id, year, last_number, created_at)
                    VALUES
                        (
                            {registryTypeId},
                            {year},
                            (
                                SELECT start_number
                                FROM dms.registry_type
                                WHERE id = {registryTypeId}
                            ),
                            NOW()
                        )
                    ON CONFLICT (registry_type_id, year)
                    DO UPDATE SET
                        last_number =
                            dms.registry_number_counter.last_number + 1,
                        updated_at = NOW()
                    RETURNING last_number AS "Value";
                    """)
                .AsEnumerable()
                .Single();

            return Task.FromResult(number);
        }

        private async Task<long> AllocateForNonRelationalTestsAsync(
            Guid registryTypeId,
            int year)
        {
            var counter = await db.RegistryNumberCounters
                .SingleOrDefaultAsync(item =>
                    item.registry_type_id == registryTypeId &&
                    item.year == year);

            if (counter is not null)
            {
                counter.last_number++;
                return counter.last_number;
            }

            var registry = await db.RegistryTypes.SingleAsync(item =>
                item.id == registryTypeId);
            counter = new RegistryNumberCounter
            {
                registry_type_id = registryTypeId,
                year = year,
                last_number = registry.start_number
            };

            db.RegistryNumberCounters.Add(counter);
            return counter.last_number;
        }
    }
}
