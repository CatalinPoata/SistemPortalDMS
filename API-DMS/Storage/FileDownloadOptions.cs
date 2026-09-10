namespace API_DMS.Storage
{
    public sealed class FileDownloadOptions
    {
        public const string SectionName = "FileDownload";

        public string SigningKey { get; set; } = null!;

        public int LifetimeMinutes { get; set; } = 15;

        public string? PublicBaseUrl { get; set; }
    }
}
