using API_DMS.Entities;
using API_DMS.Entities.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Reflection;

namespace API_DMS.Data
{
    public class DmsDbContext : DbContext
    {
        public DmsDbContext(DbContextOptions<DmsDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<UserTotp> UserTotps { get; set; } = null!;
        public DbSet<RegistryType> RegistryTypes { get; set; } = null!;
        public DbSet<DocumentKind> DocumentKinds { get; set; } = null!;
        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<RegistryNumberCounter> RegistryNumberCounters { get; set; } = null!;
        public DbSet<RegistryEntry> RegistryEntries { get; set; } = null!;
        public DbSet<RegistryDocument> RegistryDocuments { get; set; } = null!;
        public DbSet<Entities.Task> Tasks { get; set; } = null!;
        public DbSet<EntryEvent> EntryEvents { get; set; } = null!;
        public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
        public DbSet<InboxEvent> InboxEvents { get; set; } = null!;
        public DbSet<InboundRequest> InboundRequests { get; set; } = null!;
        public DbSet<ReportDefinition> ReportDefinitions { get; set; } = null!;
        public DbSet<AccountToken> AccountTokens { get; set; } = null!;
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("dms");

            modelBuilder.HasPostgresExtension("pg_trgm");








            modelBuilder.ApplyConfigurationsFromAssembly(
                Assembly.GetExecutingAssembly());
        }

        public override int SaveChanges()
        {
            SetAuditProperties();
            return base.SaveChanges();
        }

        public override async System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SetAuditProperties();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void SetAuditProperties()
        {
            var entries = ChangeTracker
                .Entries()
                .Where(e => (
                        e.State == EntityState.Added
                        || e.State == EntityState.Modified));

            foreach (var entityEntry in entries)
            {
                var entity = (BaseEntity)entityEntry.Entity;

                if (entityEntry.State == EntityState.Added)
                {
                    entity.created_at = DateTime.UtcNow;
                }
                else
                {
                    entityEntry.Property(nameof(BaseEntity.created_at)).IsModified = false;
                    entity.updated_at = DateTime.UtcNow;
                }
            }
        }
    }
}
