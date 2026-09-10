using API_PORTAL.Auth;
using Microsoft.EntityFrameworkCore.Storage;

namespace API_PORTAL_TESTS.Infrastructure
{
    public sealed class NoopRefreshSessionLock : IRefreshSessionLock
    {
        public Task<IDbContextTransaction?> AcquireAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<IDbContextTransaction?>(null);
        }
    }
}
