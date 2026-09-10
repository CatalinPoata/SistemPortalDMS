using API_PORTAL.Data;
using API_PORTAL.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace API_PORTAL.Integration
{
    public sealed class PortalOutboxDispatcher : BackgroundService
    {
        private const string RegistrationEvent = "Submission.Register";
        private const string ClarificationEvent = "Submission.AddDocuments";
        private const string CancellationEvent = "Submission.Cancel";

        private static readonly TimeSpan PollingInterval =
            TimeSpan.FromSeconds(2);

        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<PortalOutboxDispatcher> logger;

        public PortalOutboxDispatcher(
            IServiceScopeFactory scopeFactory,
            ILogger<PortalOutboxDispatcher> logger)
        {
            this.scopeFactory = scopeFactory;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(PollingInterval);

            do
            {
                try
                {
                    await DispatchDueMessagesAsync(stoppingToken);
                }
                catch (Exception exception)
                    when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(
                        exception,
                        "Expedierea outbox Portal către DMS a eșuat.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        public Task DispatchOnceAsync(CancellationToken cancellationToken)
        {
            return DispatchDueMessagesAsync(cancellationToken);
        }

        private async Task DispatchDueMessagesAsync(
            CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var client = scope.ServiceProvider.GetRequiredService<
                IDmsSubmissionRegistrationClient>();
            var clarifications = scope.ServiceProvider.GetRequiredService<
                IDmsClarificationClient>();
            var cancellations = scope.ServiceProvider.GetRequiredService<
                IDmsCancellationClient>();
            var now = DateTimeOffset.UtcNow;

            var messages = await db.OutboxMessages
                .Where(message =>
                    message.status == OutboxMessageStatus.Pending &&
                    message.next_attempt_at <= now &&
                    (message.event_type == RegistrationEvent ||
                     message.event_type == ClarificationEvent ||
                     message.event_type == CancellationEvent))
                .OrderBy(message => message.created_at)
                .Take(10)
                .ToListAsync(cancellationToken);

            foreach (var message in messages)
            {
                var result = message.event_type switch
                {
                    RegistrationEvent => await client.DeliverAsync(
                        message,
                        cancellationToken),
                    ClarificationEvent => await clarifications.DeliverAsync(
                        message,
                        cancellationToken),
                    CancellationEvent => await cancellations.DeliverAsync(
                        message,
                        cancellationToken),
                    _ => throw new InvalidOperationException(
                        "Tip de mesaj outbox Portal necunoscut.")
                };

                if (result.Delivered)
                {
                    if (message.event_type == RegistrationEvent)
                    {
                        await MarkRegistrationDeliveredAsync(
                            db,
                            message,
                            result.ResponseBody!,
                            cancellationToken);
                    }
                    else
                    {
                        await MarkDeliveredAsync(
                            db,
                            message,
                            cancellationToken);
                    }
                    continue;
                }

                message.attempts++;
                message.last_error = result.Error;

                if (!result.Retryable || message.attempts >= 8)
                {
                    message.status = OutboxMessageStatus.Failed;
                }
                else
                {
                    message.next_attempt_at = DateTimeOffset.UtcNow
                        .Add(RetryDelay(message.attempts));
                }

                await db.SaveChangesAsync(cancellationToken);
            }
        }

        private static async Task MarkRegistrationDeliveredAsync(
            PortalDbContext db,
            OutboxMessage message,
            string responseBody,
            CancellationToken cancellationToken)
        {
            var response = JsonSerializer.Deserialize<DmsRegistrationResponse>(
                responseBody,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidOperationException(
                    "DMS a trimis un răspuns de înregistrare invalid.");

            var submission = await db.Submissions.SingleOrDefaultAsync(
                item => item.id == message.aggregate_id,
                cancellationToken);

            if (submission is not null &&
                submission.status == SubmissionStatus.Submitted)
            {
                submission.status = SubmissionStatus.Registered;
                submission.dms_entry_id = response.EntryId;
                submission.registry_number = response.Number;
                submission.registry_year = response.Year;
                submission.registry_display_number = response.DisplayNumber;
                submission.registered_at = response.RegisteredAt;
                submission.status_details = null;

                db.SubmissionEvents.Add(new SubmissionEvent
                {
                    id = Guid.NewGuid(),
                    submission_id = submission.id,
                    occurred_at = response.RegisteredAt,
                    type = SubmissionEventType.Registered,
                    message = $"Cererea a fost înregistrată cu numărul " +
                        $"{response.DisplayNumber}."
                });

                db.Notifications.Add(new Notification
                {
                    id = Guid.NewGuid(),
                    user_id = submission.user_id,
                    subject = "Cererea a fost înregistrată",
                    body = $"Cererea ta a primit numărul " +
                        $"{response.DisplayNumber}.",
                    link_url = $"/cereri/{submission.id}"
                });
            }

            message.status = OutboxMessageStatus.Delivered;
            message.delivered_at = DateTimeOffset.UtcNow;
            message.last_error = null;

            await db.SaveChangesAsync(cancellationToken);
        }

        private static async Task MarkDeliveredAsync(
            PortalDbContext db,
            OutboxMessage message,
            CancellationToken cancellationToken)
        {
            message.status = OutboxMessageStatus.Delivered;
            message.delivered_at = DateTimeOffset.UtcNow;
            message.last_error = null;

            await db.SaveChangesAsync(cancellationToken);
        }

        private static TimeSpan RetryDelay(int attempts)
        {
            return attempts switch
            {
                1 => TimeSpan.FromSeconds(5),
                2 => TimeSpan.FromSeconds(30),
                3 => TimeSpan.FromMinutes(2),
                4 => TimeSpan.FromMinutes(10),
                _ => TimeSpan.FromHours(1)
            };
        }
    }
}
