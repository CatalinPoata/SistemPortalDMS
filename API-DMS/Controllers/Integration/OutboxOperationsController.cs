using API_DMS.Data;
using API_DMS.DTO.Integration;
using API_DMS.Entities;
using API_DMS.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_DMS.Controllers.Integration
{
    [ApiController]
    [Route("api/integration/outbox")]
    [Authorize(Roles = "Admin")]
    public sealed class OutboxOperationsController : ControllerBase
    {
        private readonly DmsDbContext db;

        public OutboxOperationsController(DmsDbContext db)
        {
            this.db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<OutboxMessageResponse>>> GetAll(
            [FromQuery] OutboxMessageStatus status = OutboxMessageStatus.Failed,
            CancellationToken cancellationToken = default)
        {
            var messages = await db.OutboxMessages.AsNoTracking()
                .Where(message => message.status == status)
                .OrderBy(message => message.next_attempt_at)
                .ThenBy(message => message.created_at)
                .Take(200)
                .Select(message => new OutboxMessageResponse(
                    message.id,
                    message.aggregate_type,
                    message.aggregate_id,
                    message.event_type,
                    message.status,
                    message.attempts,
                    message.next_attempt_at,
                    message.last_error,
                    message.delivered_at,
                    message.created_at))
                .ToListAsync(cancellationToken);

            return Ok(messages);
        }

        [HttpPost("{id:guid}/retry")]
        public async Task<ActionResult<OutboxMessageResponse>> Retry(
            Guid id,
            CancellationToken cancellationToken)
        {
            var message = await db.OutboxMessages.SingleOrDefaultAsync(
                item => item.id == id,
                cancellationToken);
            if (message is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Mesaj outbox inexistent",
                    "Mesajul outbox solicitat nu există."));
            }

            if (message.status != OutboxMessageStatus.Failed)
            {
                return Conflict(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    "Retry nepermis",
                    "Doar mesajele outbox eșuate pot fi relansate manual."));
            }

            message.status = OutboxMessageStatus.Pending;
            message.attempts = 0;
            message.next_attempt_at = DateTimeOffset.UtcNow;
            message.last_error = null;
            message.delivered_at = null;
            await db.SaveChangesAsync(cancellationToken);

            return Ok(ToResponse(message));
        }

        private static OutboxMessageResponse ToResponse(OutboxMessage message)
        {
            return new OutboxMessageResponse(
                message.id,
                message.aggregate_type,
                message.aggregate_id,
                message.event_type,
                message.status,
                message.attempts,
                message.next_attempt_at,
                message.last_error,
                message.delivered_at,
                message.created_at);
        }
    }
}
