using API_DMS.Data;
using API_DMS.Entities;
using API_DMS.Integration;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace API_DMS.Services
{
    public sealed class RegistryEntryWorkflowRuleException : Exception
    {
        public RegistryEntryWorkflowRuleException(string message)
            : base(message)
        {
        }
    }

    public sealed record RegistryEntryTransitionResult(
        Guid EntryId,
        EntryStatus PreviousStatus,
        EntryStatus CurrentStatus,
        string? StatusNote,
        DateTimeOffset OccurredAt);

    public sealed class RegistryEntryWorkflowService
    {
        private readonly DmsDbContext db;

        public RegistryEntryWorkflowService(DmsDbContext db)
        {
            this.db = db;
        }

        public async Task<RegistryEntryTransitionResult?> TransitionAsync(
            Guid entryId,
            EntryStatus targetStatus,
            string? note,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var entry = await db.RegistryEntries
                .SingleOrDefaultAsync(
                    x => x.id == entryId,
                    cancellationToken);

            if (entry is null)
            {
                return null;
            }

            var previousStatus = entry.status;
            var normalizedNote = NormalizeNote(note);

            if (!IsTransitionAllowed(previousStatus, targetStatus))
            {
                await AddInvalidTransitionEventAsync(
                    entry,
                    actorUserId,
                    previousStatus,
                    targetStatus,
                    normalizedNote,
                    cancellationToken);

                throw new RegistryEntryWorkflowRuleException(
                    $"Tranziția {previousStatus} → {targetStatus} nu este permisă.");
            }

            if (RequiresReason(targetStatus) &&
                normalizedNote is null)
            {
                throw new RegistryEntryWorkflowRuleException(
                    "Motivul este obligatoriu pentru această acțiune.");
            }

            if (targetStatus == EntryStatus.Completed)
            {
                var hasResponseDocument = await db.RegistryDocuments
                    .AnyAsync(
                        x => x.entry_id == entryId &&
                             x.direction == DocumentDirection.Out,
                        cancellationToken);

                if (!hasResponseDocument)
                {
                    throw new RegistryEntryWorkflowRuleException(
                        "Finalizarea necesită un document de răspuns cu direcția Out.");
                }
            }

            var occurredAt = DateTimeOffset.UtcNow;

            entry.status = targetStatus;
            entry.status_note = RequiresReason(targetStatus)
                ? normalizedNote
                : null;

            var message = GetSuccessMessage(previousStatus, targetStatus);

            db.EntryEvents.Add(new EntryEvent
            {
                id = Guid.NewGuid(),
                entry_id = entry.id,
                occurred_at = occurredAt,
                actor_user_id = actorUserId,
                type = EventType.StatusChanged,
                message = message,
                payload = JsonSerializer.SerializeToDocument(new
                {
                    previousStatus = previousStatus.ToString(),
                    currentStatus = targetStatus.ToString(),
                    note = normalizedNote
                })
            });

            if (entry.external_id.HasValue)
            {
                RegistryResponseDocument? responseDocument = null;

                if (targetStatus == EntryStatus.Completed)
                {
                    var document = await db.RegistryDocuments
                        .AsNoTracking()
                        .Where(item =>
                            item.entry_id == entry.id &&
                            item.direction == DocumentDirection.Out)
                        .OrderByDescending(item => item.created_at)
                        .FirstAsync(cancellationToken);

                    responseDocument = new RegistryResponseDocument(
                        document.id,
                        document.original_name,
                        document.content_type,
                        document.size_bytes,
                        document.sha256);
                }

                var eventId = Guid.NewGuid();
                var callback = new RegistryStatusEvent(
                    eventId,
                    entry.id,
                    entry.external_id.Value,
                    targetStatus.ToString(),
                    message,
                    entry.status_note,
                    occurredAt,
                    responseDocument);

                db.OutboxMessages.Add(new OutboxMessage
                {
                    id = eventId,
                    aggregate_type = "RegistryEntry",
                    aggregate_id = entry.id,
                    event_type = "RegistryEntry.StatusChanged",
                    payload = JsonSerializer.SerializeToDocument(
                        callback,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    status = OutboxMessageStatus.Pending,
                    attempts = 0,
                    next_attempt_at = occurredAt
                });
            }

            await db.SaveChangesAsync(cancellationToken);

            return new RegistryEntryTransitionResult(
                entry.id,
                previousStatus,
                entry.status,
                entry.status_note,
                occurredAt);
        }

        private async Task AddInvalidTransitionEventAsync(
            RegistryEntry entry,
            Guid actorUserId,
            EntryStatus previousStatus,
            EntryStatus targetStatus,
            string? note,
            CancellationToken cancellationToken)
        {
            db.EntryEvents.Add(new EntryEvent
            {
                id = Guid.NewGuid(),
                entry_id = entry.id,
                occurred_at = DateTimeOffset.UtcNow,
                actor_user_id = actorUserId,
                type = EventType.StatusChanged,
                message =
                    $"Tranziție invalidă solicitată: {previousStatus} → {targetStatus}.",
                payload = JsonSerializer.SerializeToDocument(new
                {
                    previousStatus = previousStatus.ToString(),
                    requestedStatus = targetStatus.ToString(),
                    note,
                    succeeded = false
                })
            });

            await db.SaveChangesAsync(cancellationToken);
        }

        private static bool IsTransitionAllowed(
            EntryStatus previousStatus,
            EntryStatus targetStatus)
        {
            return (previousStatus, targetStatus) switch
            {
                (EntryStatus.Submitted, EntryStatus.Registered) => true,
                (EntryStatus.Submitted, EntryStatus.Cancelled) => true,

                (EntryStatus.Registered, EntryStatus.InReview) => true,
                (EntryStatus.Registered, EntryStatus.Cancelled) => true,

                (EntryStatus.InReview, EntryStatus.InfoRequested) => true,
                (EntryStatus.InReview, EntryStatus.Completed) => true,
                (EntryStatus.InReview, EntryStatus.Rejected) => true,

                (EntryStatus.InfoRequested, EntryStatus.InReview) => true,
                (EntryStatus.InfoRequested, EntryStatus.Cancelled) => true,

                _ => false
            };
        }

        private static bool RequiresReason(EntryStatus status)
        {
            return status is EntryStatus.InfoRequested
                or EntryStatus.Rejected
                or EntryStatus.Cancelled;
        }

        private static string? NormalizeNote(string? note)
        {
            return string.IsNullOrWhiteSpace(note)
                ? null
                : note.Trim();
        }

        private static string GetSuccessMessage(
            EntryStatus previousStatus,
            EntryStatus targetStatus)
        {
            return (previousStatus, targetStatus) switch
            {
                (EntryStatus.Registered, EntryStatus.InReview) =>
                    "Poziția a fost preluată spre soluționare.",

                (EntryStatus.InfoRequested, EntryStatus.InReview) =>
                    "Poziția a revenit în lucru.",

                (_, EntryStatus.InfoRequested) =>
                    "Au fost solicitate clarificări.",

                (_, EntryStatus.Rejected) =>
                    "Poziția a fost respinsă.",

                (_, EntryStatus.Completed) =>
                    "Poziția a fost finalizată.",

                (_, EntryStatus.Cancelled) =>
                    "Poziția a fost anulată.",

                _ =>
                    "Starea poziției a fost modificată."
            };
        }
    }
}
