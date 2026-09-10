using API_PORTAL.Integration;

namespace API_PORTAL_TESTS.Infrastructure
{
    public sealed class FakeDmsRegistrationReceiptClient
        : IDmsRegistrationReceiptClient
    {
        public DmsReceiptDownloadResult Result { get; set; } = new(
            DmsReceiptDownloadStatus.Unavailable,
            null,
            "Clientul DMS nu a fost configurat pentru test.");

        public Guid? LastEntryId { get; private set; }

        public int DownloadCount { get; private set; }

        public void Reset()
        {
            LastEntryId = null;
            DownloadCount = 0;
            Result = new DmsReceiptDownloadResult(
                DmsReceiptDownloadStatus.Unavailable,
                null,
                "Clientul DMS nu a fost configurat pentru test.");
        }

        public Task<DmsReceiptDownloadResult> DownloadAsync(
            Guid entryId,
            CancellationToken cancellationToken)
        {
            LastEntryId = entryId;
            DownloadCount++;
            return Task.FromResult(Result);
        }
    }
}
