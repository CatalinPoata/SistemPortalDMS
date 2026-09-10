namespace API_PORTAL.Storage
{
    public sealed class SubmissionFileDownloadOptions
    {
        public const string SectionName = "FileDownload";

        public string SigningKey { get; set; } = null!;

        public int LifetimeMinutes { get; set; } = 15;
    }
}
