using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace API_PORTAL.Data
{
    public class PortalDbContext : DbContext
    {
        public PortalDbContext(DbContextOptions<PortalDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<UserTotp> UserTotps { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<ServiceDefinition> ServiceDefinitions { get; set; } = null!;
        public DbSet<Submission> Submissions { get; set; } = null!;
        public DbSet<SubmissionFile> SubmissionFiles { get; set; } = null!;
        public DbSet<SubmissionEvent> SubmissionEvents { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<Article> Articles { get; set; }
        public DbSet<PublicRegistry> PublicRegistrations { get; set; }
        public DbSet<PublicRegistryEntry> PublicRegistryEntries { get; set; }
        public DbSet<PublicRegistryDocument> PublicRegistryDocuments { get; set; }
        public DbSet<Survey> Surveys { get; set; }
        public DbSet<SurveyQuestion> SurveyQuestions { get; set; }
        public DbSet<SurveyResponse> SurveyResponses { get; set; }
        public DbSet<AppointmentType> AppointmentTypes { get; set; }
        public DbSet<AppointmentSlot> AppointmentSlots { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<InboxEvent> InboxEvents { get; set; }
        public DbSet<InboundRequest> InboundRequests { get; set; }
        public DbSet<ReportDefinition> ReportDefinitions { get; set; }
        public DbSet<AccountToken> AccountTokens { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("portal");














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
