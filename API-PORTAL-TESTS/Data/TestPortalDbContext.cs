using API_PORTAL.Data;
using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace API_PORTAL_TESTS.Data
{
    internal sealed class TestPortalDbContext
    : PortalDbContext
    {
        public TestPortalDbContext(
            DbContextOptions<PortalDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var jsonConverter =
                new ValueConverter<JsonDocument, string>(
                    document =>
                        document.RootElement.GetRawText(),

                    json =>
                        JsonDocument.Parse(
                            json,
                            new JsonDocumentOptions()));

            var nullableJsonConverter =
                new ValueConverter<JsonDocument?, string?>(
                    document =>
                        document == null
                            ? null
                            : document.RootElement.GetRawText(),

                    json =>
                        string.IsNullOrWhiteSpace(json)
                            ? null
                            : JsonDocument.Parse(
                                json,
                                new JsonDocumentOptions()));

            modelBuilder.Entity<ServiceDefinition>()
                .Property(item => item.form_schema)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<Submission>()
                .Property(item => item.form_snapshot)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<Submission>()
                .Property(item => item.values)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<OutboxMessage>()
                .Property(item => item.payload)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<InboundRequest>()
                .Property(item => item.response_body)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<ReportDefinition>()
                .Property(item => item.definition)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<SurveyResponse>()
                .Property(item => item.answers)
                .HasConversion(jsonConverter);

            modelBuilder.Entity<SurveyQuestion>()
                .Property(item => item.options)
                .HasConversion(nullableJsonConverter);

            modelBuilder.Entity<UserTotp>()
                .Property(item => item.recovery_code_hashes)
                .HasConversion(jsonConverter);
        }
    }
}
