using API_PORTAL.Data;
using API_PORTAL.Entities;
using API_PORTAL.Errors;
using API_PORTAL.Integration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_PORTAL.Controllers.Integration
{
    [ApiController]
    [Route("api/callbacks/registry-events")]
    [ServiceAuthentication]
    public sealed class RegistryEventsCallbackController : ControllerBase
    {
        private const string EventType = "RegistryEntry.StatusChanged";

        private readonly PortalDbContext db;

        public RegistryEventsCallbackController(PortalDbContext db)
        {
            this.db = db;
        }

        [HttpPost]
        public async Task<IActionResult> Receive(
            DmsRegistryEventCallback callback,
            CancellationToken cancellationToken)
        {
            if (callback.EventId == Guid.Empty)
            {
                return UnprocessableEntity(InvalidCallback());
            }

            var alreadyProcessed = await db.InboxEvents
                .AsNoTracking()
                .AnyAsync(item => item.event_id == callback.EventId,
                    cancellationToken);

            if (alreadyProcessed)
            {
                return Ok(new { received = true });
            }

            if (!TryValidate(callback, out var status))
            {
                return UnprocessableEntity(InvalidCallback());
            }

            var submission = await db.Submissions.SingleOrDefaultAsync(
                item => item.external_id == callback.ExternalId,
                cancellationToken);

            if (submission is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Cerere inexistentă",
                    "Cererea Portal corespunzătoare evenimentului nu există."));
            }

            if (submission.dms_entry_id.HasValue &&
                submission.dms_entry_id.Value != callback.EntryId)
            {
                return Conflict(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    "Corelare DMS invalidă",
                    "Evenimentul nu corespunde poziției DMS a cererii."));
            }

            submission.dms_entry_id ??= callback.EntryId;
            submission.status = status;
            submission.status_details = StatusDetails(callback, status);
            var message = DisplayMessage(callback, status);

            Guid? responseFileId = null;
            if (callback.ResponseDocument is not null)
            {
                responseFileId = callback.ResponseDocument.DocumentId;
                var hasResponse = await db.SubmissionFiles.AnyAsync(
                    item => item.id == responseFileId.Value,
                    cancellationToken);

                if (!hasResponse)
                {
                    db.SubmissionFiles.Add(new SubmissionFile
                    {
                        id = responseFileId.Value,
                        submission_id = submission.id,
                        kind = SubmissionFileKind.Response,
                        storage_key = $"dms/{responseFileId.Value:N}",
                        original_name = callback.ResponseDocument.Name!.Trim(),
                        content_type = callback.ResponseDocument.ContentType!.Trim(),
                        size_bytes = callback.ResponseDocument.SizeBytes,
                        sha256 = callback.ResponseDocument.Sha256!.Trim()
                    });
                }
            }

            db.SubmissionEvents.Add(new SubmissionEvent
            {
                id = Guid.NewGuid(),
                submission_id = submission.id,
                occurred_at = callback.OccurredAt.ToUniversalTime(),
                type = ToSubmissionEventType(status),
                message = message,
                file_id = responseFileId
            });

            db.Notifications.Add(new Notification
            {
                id = Guid.NewGuid(),
                user_id = submission.user_id,
                subject = NotificationSubject(status),
                body = message,
                link_url = $"/cereri/{submission.id}"
            });

            var rawBody = HttpContext.Items[
                ServiceAuthenticationFilter.RawBodyItemKey] as string ?? "{}";
            db.InboxEvents.Add(new InboxEvent
            {
                event_id = callback.EventId,
                source = "DMS",
                event_type = EventType,
                payload = rawBody,
                processed_at = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync(cancellationToken);

            return Ok(new { received = true });
        }

        private static bool TryValidate(
            DmsRegistryEventCallback callback,
            out SubmissionStatus status)
        {
            status = default;

            if (callback.EntryId == Guid.Empty ||
                callback.ExternalId == Guid.Empty ||
                string.IsNullOrWhiteSpace(callback.Status) ||
                string.IsNullOrWhiteSpace(callback.Message) ||
                callback.Message.Length > 1000 ||
                callback.StatusNote?.Length > 1000 ||
                callback.OccurredAt == default ||
                !Enum.TryParse(callback.Status, out status) ||
                !Enum.IsDefined(status) ||
                status == SubmissionStatus.Submitted)
            {
                return false;
            }

            var response = callback.ResponseDocument;
            if (status == SubmissionStatus.Completed && response is null)
            {
                return false;
            }

            return response is null ||
                (response.DocumentId != Guid.Empty &&
                 !string.IsNullOrWhiteSpace(response.Name) &&
                 response.Name.Length <= 255 &&
                 !string.IsNullOrWhiteSpace(response.ContentType) &&
                 response.ContentType.Length <= 120 &&
                 response.SizeBytes > 0 &&
                 !string.IsNullOrWhiteSpace(response.Sha256) &&
                 response.Sha256.Length == 64);
        }

        private static SubmissionEventType ToSubmissionEventType(
            SubmissionStatus status)
        {
            return status switch
            {
                SubmissionStatus.InfoRequested => SubmissionEventType.InfoRequested,
                SubmissionStatus.Completed => SubmissionEventType.Completed,
                SubmissionStatus.Rejected => SubmissionEventType.Rejected,
                SubmissionStatus.Cancelled => SubmissionEventType.Cancelled,
                _ => SubmissionEventType.StatusChanged
            };
        }

        private static string StatusDetails(
            DmsRegistryEventCallback callback,
            SubmissionStatus status)
        {
            if (status is (SubmissionStatus.InfoRequested or
                SubmissionStatus.Rejected or SubmissionStatus.Cancelled) &&
                !string.IsNullOrWhiteSpace(callback.StatusNote))
            {
                return callback.StatusNote.Trim();
            }

            return callback.Message!.Trim();
        }

        private static string DisplayMessage(
            DmsRegistryEventCallback callback,
            SubmissionStatus status)
        {
            var message = callback.Message!.Trim();

            if (status is not (SubmissionStatus.InfoRequested or
                SubmissionStatus.Rejected or SubmissionStatus.Cancelled) ||
                string.IsNullOrWhiteSpace(callback.StatusNote))
            {
                return message;
            }

            return $"{message} Motiv: {callback.StatusNote.Trim()}";
        }

        private static string NotificationSubject(SubmissionStatus status)
        {
            return status switch
            {
                SubmissionStatus.InReview => "Cererea este în lucru",
                SubmissionStatus.InfoRequested => "Sunt necesare clarificări",
                SubmissionStatus.Completed => "Cererea a fost soluționată",
                SubmissionStatus.Rejected => "Cererea a fost respinsă",
                SubmissionStatus.Cancelled => "Cererea a fost anulată",
                _ => "Starea cererii a fost actualizată"
            };
        }

        private ProblemDetails InvalidCallback()
        {
            return ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Eveniment DMS invalid",
                "Corpul callback-ului nu respectă contractul de integrare.");
        }
    }
}
