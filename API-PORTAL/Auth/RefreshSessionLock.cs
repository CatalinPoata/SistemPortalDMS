using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using Microsoft.EntityFrameworkCore;
using API_PORTAL.Data;


namespace API_PORTAL.Auth
{
    public interface IRefreshSessionLock
    {
        Task<IDbContextTransaction?> AcquireAsync(
            Guid userId,
            CancellationToken cancellationToken);
    }

    public sealed class PostgresRefreshSessionLock
        : IRefreshSessionLock
    {
        private readonly PortalDbContext db;

        public PostgresRefreshSessionLock(PortalDbContext db)
        {
            this.db = db;
        }

        public async Task<IDbContextTransaction?> AcquireAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            if (!db.Database.IsNpgsql())
            {
                throw new InvalidOperationException(
                    "Blocarea sesiunii necesită PostgreSQL.");
            }

            var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                await db.Database.SqlQuery<Guid>($"""
                SELECT id AS "Value"
                FROM portal."user"
                WHERE id = {userId}
                FOR UPDATE
                """)
                    .ToListAsync(cancellationToken);

                return transaction;
            }
            catch
            {
                await transaction.DisposeAsync();
                throw;
            }
        }
    }
}
