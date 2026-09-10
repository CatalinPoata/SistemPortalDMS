using API_DMS.Data;
using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace API_DMS_TESTS.Data
{
    internal sealed class TestDmsDbContext
    : DmsDbContext
    {
        public TestDmsDbContext(
            DbContextOptions<DmsDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var jsonDocumentConverter =
                new ValueConverter<JsonDocument, string>(
                    document =>
                        document.RootElement.GetRawText(),

                    json =>
                        JsonDocument.Parse(
                            json,
                            new JsonDocumentOptions()));

            var nullableJsonDocumentConverter =
                new ValueConverter<JsonDocument?, string>(
                    document =>
                        document!.RootElement.GetRawText(),

                    json =>
                        JsonDocument.Parse(
                            json,
                            new JsonDocumentOptions()));

            modelBuilder.Entity<EntryEvent>()
                .Property(item => item.payload)
                .HasConversion(jsonDocumentConverter);

            modelBuilder.Entity<InboundRequest>()
                .Property(item => item.response_body)
                .HasConversion(jsonDocumentConverter);

            modelBuilder.Entity<OutboxMessage>()
                .Property(item => item.payload)
                .HasConversion(jsonDocumentConverter);

            modelBuilder.Entity<ReportDefinition>()
                .Property(item => item.definition)
                .HasConversion(jsonDocumentConverter);

            modelBuilder.Entity<RegistryEntry>()
                .Property(item => item.form_values)
                .HasConversion(nullableJsonDocumentConverter);

            modelBuilder.Entity<UserTotp>()
                .Property(item => item.recovery_code_hashes)
                .HasConversion(jsonDocumentConverter);
        }
    }
}
