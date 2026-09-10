using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace API_PORTAL.Storage
{
    public sealed record StoredSubmissionFile(
        string StorageKey,
        string ContentType,
        long SizeBytes,
        string Sha256);

    public sealed class InvalidSubmissionFileException : Exception
    {
        public InvalidSubmissionFileException(string message)
            : base(message)
        {
        }
    }

    public sealed class SubmissionFileStorageService
    {
        public const long MaxFileSize = 10L * 1024 * 1024;
        public const long MaxSubmissionRequestSize =
            256L * 1024 * 1024;

        private readonly string rootPath;
        private readonly SubmissionFileContentDetector detector;

        public SubmissionFileStorageService(
            IHostEnvironment environment,
            IOptions<SubmissionFileStorageOptions> options,
            SubmissionFileContentDetector detector)
        {
            rootPath = Path.IsPathRooted(options.Value.RootPath)
                ? options.Value.RootPath
                : Path.Combine(
                    environment.ContentRootPath,
                    options.Value.RootPath);

            this.detector = detector;
        }

        public async Task<StoredSubmissionFile> StoreAsync(
            IFormFile upload,
            long maximumSize,
            CancellationToken cancellationToken)
        {
            var effectiveMaximum = Math.Min(
                maximumSize,
                MaxFileSize);

            if (upload.Length <= 0)
            {
                throw new InvalidSubmissionFileException(
                    "Fișierul este gol.");
            }

            if (upload.Length > effectiveMaximum)
            {
                throw new InvalidSubmissionFileException(
                    SizeMessage(effectiveMaximum));
            }

            var storageId = Guid.NewGuid();
            var storageKey =
                $"{DateTime.UtcNow:yyyy/MM}/{storageId:N}";
            var temporaryDirectory = Path.Combine(rootPath, ".tmp");

            Directory.CreateDirectory(temporaryDirectory);

            var temporaryPath = Path.Combine(
                temporaryDirectory,
                $"{storageId:N}.tmp");

            try
            {
                long size = 0;
                using var hash = IncrementalHash.CreateHash(
                    HashAlgorithmName.SHA256);

                await using (var input = upload.OpenReadStream())
                await using (var output = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous))
                {
                    var buffer = new byte[81920];
                    int bytesRead;

                    while ((bytesRead = await input.ReadAsync(
                        buffer.AsMemory(),
                        cancellationToken)) > 0)
                    {
                        size += bytesRead;

                        if (size > effectiveMaximum)
                        {
                            throw new InvalidSubmissionFileException(
                                SizeMessage(effectiveMaximum));
                        }

                        hash.AppendData(buffer, 0, bytesRead);
                        await output.WriteAsync(
                            buffer.AsMemory(0, bytesRead),
                            cancellationToken);
                    }
                }

                DetectedSubmissionFileType? detected;

                await using (var validationStream = new FileStream(
                    temporaryPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.Asynchronous))
                {
                    detected = await detector.DetectAsync(
                        validationStream,
                        cancellationToken);
                }

                if (detected is null)
                {
                    throw new InvalidSubmissionFileException(
                        "Tipul fișierului nu este permis sau fișierul este invalid.");
                }

                var finalPath = GetFullPath(storageKey);
                Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
                File.Move(temporaryPath, finalPath);

                return new StoredSubmissionFile(
                    storageKey,
                    detected.ContentType,
                    size,
                    Convert.ToHexString(hash.GetHashAndReset())
                        .ToLowerInvariant());
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public Task DeleteAsync(string storageKey)
        {
            var path = GetFullPath(storageKey);

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        public FileStream OpenRead(string storageKey)
        {
            return new FileStream(
                GetFullPath(storageKey),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }

        private string GetFullPath(string storageKey)
        {
            var normalizedKey = storageKey.Replace(
                '/',
                Path.DirectorySeparatorChar);
            var root = Path.GetFullPath(rootPath);
            var fullPath = Path.GetFullPath(
                Path.Combine(root, normalizedKey));
            var rootWithSeparator =
                root.EndsWith(Path.DirectorySeparatorChar)
                    ? root
                    : root + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(
                rootWithSeparator,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Storage key invalid.");
            }

            return fullPath;
        }

        private static string SizeMessage(long maximumSize)
        {
            var maximumMb = maximumSize / (1024 * 1024);

            return $"Fișierul depășește limita de {maximumMb} MB.";
        }
    }
}
