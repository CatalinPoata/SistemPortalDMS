using API_DMS.Data;
using API_DMS.Entities;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace API_DMS.Integration
{
    public sealed class DmsOutboxDispatcher : BackgroundService
    {
        private const string RegistryStatusChanged =
            "RegistryEntry.StatusChanged";

        private static readonly TimeSpan PollingInterval =
            TimeSpan.FromSeconds(2);

        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<DmsOutboxDispatcher> logger;

        public DmsOutboxDispatcher(
            IServiceScopeFactory scopeFactory,
            ILogger<DmsOutboxDispatcher> logger)
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
                        "Expedierea outbox DMS către Portal a eșuat.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task DispatchDueMessagesAsync(
            CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
            var client = scope.ServiceProvider.GetRequiredService<
                IPortalRegistryEventCallbackClient>();
            var now = DateTimeOffset.UtcNow;

            var messages = await db.OutboxMessages
                .Where(message =>
                    message.status == OutboxMessageStatus.Pending &&
                    message.next_attempt_at <= now &&
                    message.event_type == RegistryStatusChanged)
                .OrderBy(message => message.created_at)
                .Take(10)
                .ToListAsync(cancellationToken);

            foreach (var message in messages)
            {
                var result = await client.DeliverAsync(
                    message,
                    cancellationToken);

                if (result.Delivered)
                {
                    message.status = OutboxMessageStatus.Delivered;
                    message.delivered_at = DateTimeOffset.UtcNow;
                    message.last_error = null;
                }
                else
                {
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
                }

                await db.SaveChangesAsync(cancellationToken);
            }
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
