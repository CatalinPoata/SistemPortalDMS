using API_PORTAL.Integration;

namespace API_PORTAL_TESTS.Infrastructure
{
    public sealed class FakeDmsRegistryTypeClient
        : IDmsRegistryTypeClient
    {
        private readonly Dictionary<string, DmsRegistryTypeListItem> registries = new(
            StringComparer.OrdinalIgnoreCase)
        {
            ["INTRARI"] = new(
                "INTRARI",
                "Registru de intrări",
                "In",
                false)
        };

        public void SetRegistry(
            string code,
            bool isClosed = false,
            string? name = null,
            string direction = "In")
        {
            registries[code] = new DmsRegistryTypeListItem(
                code,
                name ?? code,
                direction,
                isClosed);
        }

        public Task<RegistryTypeListResult> GetAllAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new RegistryTypeListResult(
                true,
                registries.Values
                    .OrderBy(item => item.Code)
                    .ToArray()));
        }

        public Task<RegistryTypeLookupResult> GetAsync(
            string code,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                registries.TryGetValue(code, out var registry)
                    ? new RegistryTypeLookupResult(
                        RegistryTypeLookupStatus.Found,
                        registry.IsClosed)
                    : RegistryTypeLookupResult.Missing);
        }
    }
}
