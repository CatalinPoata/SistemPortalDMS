using API_DMS.Auth;
using Microsoft.EntityFrameworkCore.Storage;

namespace API_DMS_TESTS.Infrastructure
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
