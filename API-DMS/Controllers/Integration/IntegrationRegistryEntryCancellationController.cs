using API_DMS.Data;
using API_DMS.Entities;
using API_DMS.Entities.Base;
using API_DMS.Errors;
using API_DMS.Integration;
using API_DMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace API_DMS.Controllers.Integration
{
    [ApiController]
    [Route("api/integration/registry-entries")]
    [ServiceAuthentication]
    public sealed class IntegrationRegistryEntryCancellationController
        : ControllerBase
    {
        private const string Endpoint =
            "/api/integration/registry-entries/{entryId}/cancel";

        private readonly DmsDbContext db;
        private readonly RegistryEntryWorkflowService workflow;
        private readonly PortalIntegrationOptions options;

        public IntegrationRegistryEntryCancellationController(
            DmsDbContext db,
            RegistryEntryWorkflowService workflow,
            IOptions<PortalIntegrationOptions> options)
        {
            this.db = db;
            this.workflow = workflow;
            this.options = options.Value;
        }

        [HttpPost("{entryId:guid}/cancel")]
        public async Task<IActionResult> Cancel(
            Guid entryId,
            PortalCancellationRequest request,
            CancellationToken cancellationToken)
        {
            var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(idempotencyKey) ||
                idempotencyKey.Length > 100)
            {
                return BadRequest(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    "Cheie de idempotență invalidă",
                    "Antetul Idempotency-Key este obligatoriu și are maximum 100 caractere."));
            }

            var rawBody = HttpContext.Items[
                ServiceAuthenticationFilter.RawBodyItemKey] as string ?? string.Empty;
            var requestHash = Hash(rawBody);
            var existing = await db.InboundRequests.AsNoTracking()
                .SingleOrDefaultAsync(item =>
                    item.endpoint == Endpoint &&
                    item.idempotency_key == idempotencyKey,
                    cancellationToken);
            if (existing is not null)
            {
                if (!FixedTimeEquals(existing.request_hash, requestHash))
                {
                    return Conflict(ApiProblemDetails.Create(
                        HttpContext,
                        StatusCodes.Status409Conflict,
                        "Cheie de idempotență reutilizată",
                        "Aceeași cheie a fost trimisă anterior cu un corp diferit."));
                }

                return JsonResult(
                    existing.response_status,
                    existing.response_body.RootElement.GetRawText());
            }

            try
            {
                var response = await CancelAsync(
                    entryId,
                    request,
                    idempotencyKey,
                    requestHash,
                    cancellationToken);
                return JsonResult(
                    StatusCodes.Status200OK,
                    JsonSerializer.Serialize(
                        response,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            }
            catch (PortalIntegrationRuleException exception)
            {
                return UnprocessableEntity(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Retragere Portal invalidă",
                    exception.Message));
            }
            catch (RegistryEntryWorkflowRuleException exception)
            {
                return UnprocessableEntity(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Retragere nepermisă",
                    exception.Message));
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException postgres &&
                      postgres.SqlState == "23505")
            {
                var replay = await db.InboundRequests.AsNoTracking()
                    .SingleOrDefaultAsync(item =>
                        item.endpoint == Endpoint &&
                        item.idempotency_key == idempotencyKey,
                        cancellationToken);
                if (replay is not null &&
                    FixedTimeEquals(replay.request_hash, requestHash))
                {
                    return JsonResult(
                        replay.response_status,
                        replay.response_body.RootElement.GetRawText());
                }

                throw;
            }
        }

        private async Task<PortalCancellationResponse> CancelAsync(
            Guid entryId,
            PortalCancellationRequest request,
            string idempotencyKey,
            string requestHash,
            CancellationToken cancellationToken)
        {
            if (entryId == Guid.Empty ||
                request.EntryId != entryId ||
                request.ExternalId == Guid.Empty ||
                string.IsNullOrWhiteSpace(request.Reason) ||
                request.Reason.Length > 1000)
            {
                throw new PortalIntegrationRuleException(
                    "Corpul retragerii Portal nu respectă contractul de integrare.");
            }

            var actorEmail = options.ActorEmail.Trim().ToLowerInvariant();
            var actor = await db.Users.SingleOrDefaultAsync(user =>
                user.email == actorEmail && user.is_active &&
                (user.role == Role.Clerk || user.role == Role.Admin),
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "Utilizatorul configurat pentru integrarea Portal nu există sau nu este activ.");

            var entry = await db.RegistryEntries.SingleOrDefaultAsync(item =>
                item.id == entryId && item.external_id == request.ExternalId,
                cancellationToken)
                ?? throw new PortalIntegrationRuleException(
                    "Poziția DMS nu corespunde cererii Portal.");

            if (entry.status is not (EntryStatus.Registered or EntryStatus.InfoRequested))
            {
                throw new PortalIntegrationRuleException(
                    "Poziția DMS nu mai poate fi retrasă în starea curentă.");
            }

            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(cancellationToken)
                : null;
            var transition = await workflow.TransitionAsync(
                entryId,
                EntryStatus.Cancelled,
                request.Reason.Trim(),
                actor.id,
                cancellationToken)
                ?? throw new PortalIntegrationRuleException(
                    "Poziția DMS nu mai există.");

            var response = new PortalCancellationResponse(
                entryId,
                EntryStatus.Cancelled.ToString(),
                transition.OccurredAt);
            db.InboundRequests.Add(new InboundRequest
            {
                endpoint = Endpoint,
                idempotency_key = idempotencyKey,
                request_hash = requestHash,
                response_status = StatusCodes.Status200OK,
                response_body = JsonSerializer.SerializeToDocument(
                    response,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return response;
        }

        private static string Hash(string value)
        {
            return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        }

        private static bool FixedTimeEquals(string first, string second)
        {
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(first),
                Encoding.UTF8.GetBytes(second));
        }

        private static ContentResult JsonResult(int statusCode, string body)
        {
            return new ContentResult
            {
                StatusCode = statusCode,
                ContentType = "application/json; charset=utf-8",
                Content = body
            };
        }
    }
}
