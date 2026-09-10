using API_PORTAL.Entities;
using API_PORTAL.Integration;
using System.Collections.Concurrent;

namespace API_PORTAL_TESTS.Infrastructure
{
    public sealed class FakeDmsSubmissionRegistrationClient
        : IDmsSubmissionRegistrationClient
    {
        private readonly ConcurrentQueue<DmsRegistrationDeliveryResult> results =
            new();
        private int deliveryCount;

        public int DeliveryCount => deliveryCount;

        public void Enqueue(DmsRegistrationDeliveryResult result)
        {
            results.Enqueue(result);
        }

        public void Reset()
        {
            while (results.TryDequeue(out _))
            {
            }

            Interlocked.Exchange(ref deliveryCount, 0);
        }

        public Task<DmsRegistrationDeliveryResult> DeliverAsync(
            OutboxMessage message,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref deliveryCount);

            if (!results.TryDequeue(out var result))
            {
                throw new InvalidOperationException(
                    "Nu a fost configurat un răspuns DMS pentru test.");
            }

            return Task.FromResult(result);
        }
    }
}
