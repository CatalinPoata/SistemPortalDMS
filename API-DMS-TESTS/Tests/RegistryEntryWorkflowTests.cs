using API_DMS.Entities;
using API_DMS.Services;
using API_DMS_TESTS.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace API_DMS_TESTS.Tests
{
    public sealed class RegistryEntryWorkflowTests
    {
        [Fact]
        public async Task T6_Invalid_transition_creates_audit_event()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await using var db =
                TestDbContextFactory.CreateInMemory();

            var actorId = Guid.NewGuid();

            var entry = CreateEntry(
                EntryStatus.Completed);

            db.RegistryEntries.Add(entry);

            await db.SaveChangesAsync(cancellationToken);

            var service =
                new RegistryEntryWorkflowService(db);

            var exception =
                await Assert.ThrowsAsync<
                    RegistryEntryWorkflowRuleException>(
                    () => service.TransitionAsync(
                        entry.id,
                        EntryStatus.InReview,
                        null,
                        actorId,
                        cancellationToken));

            Assert.Contains(
                "nu este permisă",
                exception.Message);

            var auditEvent =
                await db.EntryEvents.SingleAsync(cancellationToken);

            Assert.Equal(
                EventType.StatusChanged,
                auditEvent.type);

            Assert.False(
                auditEvent.payload.RootElement
                    .GetProperty("succeeded")
                    .GetBoolean());

            var persistedEntry =
                await db.RegistryEntries
                    .SingleAsync(cancellationToken);

            Assert.Equal(
                EntryStatus.Completed,
                persistedEntry.status);
        }

        [Fact]
        public async Task T7_Complete_without_out_document_returns_422_rule()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var db =
                TestDbContextFactory.CreateInMemory();

            var entry = CreateEntry(
                EntryStatus.InReview);

            db.RegistryEntries.Add(entry);

            await db.SaveChangesAsync(cancellationToken);

            var service =
                new RegistryEntryWorkflowService(db);

            var exception =
                await Assert.ThrowsAsync<
                    RegistryEntryWorkflowRuleException>(
                    () => service.TransitionAsync(
                        entry.id,
                        EntryStatus.Completed,
                        null,
                        Guid.NewGuid(),
                        cancellationToken));

            Assert.Contains(
                "document de răspuns",
                exception.Message);

            var persistedEntry =
                await db.RegistryEntries
                    .SingleAsync(cancellationToken);

            Assert.Equal(
                EntryStatus.InReview,
                persistedEntry.status);

            Assert.Empty(
                await db.EntryEvents.ToListAsync(cancellationToken));
        }

        [Fact]
        public async Task Portal_entry_transition_writes_callback_to_outbox()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await using var db = TestDbContextFactory.CreateInMemory();
            var externalId = Guid.NewGuid();
            var entry = CreateEntry(EntryStatus.Registered);
            entry.external_id = externalId;
            db.RegistryEntries.Add(entry);
            await db.SaveChangesAsync(cancellationToken);

            var service = new RegistryEntryWorkflowService(db);
            var result = await service.TransitionAsync(
                entry.id,
                EntryStatus.InReview,
                null,
                Guid.NewGuid(),
                cancellationToken);

            Assert.NotNull(result);

            var outbox = await db.OutboxMessages.SingleAsync(
                cancellationToken);
            Assert.Equal("RegistryEntry", outbox.aggregate_type);
            Assert.Equal(entry.id, outbox.aggregate_id);
            Assert.Equal("RegistryEntry.StatusChanged", outbox.event_type);
            Assert.Equal(OutboxMessageStatus.Pending, outbox.status);
            Assert.Equal(externalId, outbox.payload.RootElement
                .GetProperty("externalId").GetGuid());
            Assert.Equal("InReview", outbox.payload.RootElement
                .GetProperty("status").GetString());
        }

        private static RegistryEntry CreateEntry(
            EntryStatus status)
        {
            return new RegistryEntry
            {
                id = Guid.NewGuid(),
                registry_type_id = Guid.NewGuid(),
                year = 2026,
                number = 1,
                direction = EntryDirection.In,
                registered_at = DateTimeOffset.UtcNow,
                subject = "Test automat",
                applicant_name = "Applicant Test",
                deadline = DateOnly.FromDateTime(
                    DateTime.UtcNow),
                status = status,
                created_by_user_id = Guid.NewGuid()
            };
        }
    }
}
