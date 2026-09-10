using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace API_DMS.Storage
{
    public sealed record StoredFile(
    string StorageKey,
    string ContentType,
    long SizeBytes,
    string Sha256);

    public sealed class InvalidStoredFileException : Exception
    {
        public InvalidStoredFileException(string message)
            : base(message)
        {
        }
    }

    public sealed class FileStorageService
    {
        public const long MaxFileSize = 10L * 1024 * 1024;
        public const long MultipartOverhead = 64L * 1024;
        public const long MaxUploadRequestSize =
            MaxFileSize + MultipartOverhead;

        private readonly string rootPath;
        private readonly FileContentDetector detector;

        public FileStorageService(
            IHostEnvironment environment,
            IOptions<FileStorageOptions> options,
            FileContentDetector detector)
        {
            rootPath = Path.IsPathRooted(options.Value.RootPath)
                ? options.Value.RootPath
                : Path.Combine(
                    environment.ContentRootPath,
                    options.Value.RootPath);

            this.detector = detector;
        }

        public async Task<StoredFile> StoreAsync(
            IFormFile upload,
            CancellationToken cancellationToken)
        {
            if (upload.Length <= 0)
            {
                throw new InvalidStoredFileException(
                    "Fișierul este gol.");
            }

            if (upload.Length > MaxFileSize)
            {
                throw new InvalidStoredFileException(
                    "Fișierul depășește limita de 10 MB.");
            }

            await using var input = upload.OpenReadStream();

            return await StoreAsync(input, cancellationToken);
        }

        public async Task<StoredFile> StoreAsync(
            Stream input,
            CancellationToken cancellationToken)
        {
            if (!input.CanRead)
            {
                throw new InvalidStoredFileException(
                    "Conținutul fișierului nu poate fi citit.");
            }

            var fileId = Guid.NewGuid();

            var storageKey =
                $"{DateTime.UtcNow:yyyy/MM}/{fileId:N}";

            var temporaryDirectory =
                Path.Combine(rootPath, ".tmp");

            Directory.CreateDirectory(temporaryDirectory);

            var temporaryPath = Path.Combine(
                temporaryDirectory,
                $"{fileId:N}.tmp");

            try
            {
                long size = 0;

                using var hash = IncrementalHash.CreateHash(
                    HashAlgorithmName.SHA256);

                await using (
                    var output = new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920,
                        options: FileOptions.Asynchronous))
                {
                    var buffer = new byte[81920];
                    int bytesRead;

                    while ((bytesRead = await input.ReadAsync(
                        buffer.AsMemory(),
                        cancellationToken)) > 0)
                    {
                        size += bytesRead;

                        if (size > MaxFileSize)
                        {
                            throw new InvalidStoredFileException(
                                "Fișierul depășește limita de 10 MB.");
                        }

                        hash.AppendData(buffer, 0, bytesRead);

                        await output.WriteAsync(
                            buffer.AsMemory(0, bytesRead),
                            cancellationToken);
                    }
                }

                DetectedFileType? detectedType;

                await using (var validationStream =
                    new FileStream(
                        temporaryPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 81920,
                        options: FileOptions.Asynchronous))
                {
                    detectedType = await detector.DetectAsync(
                        validationStream,
                        cancellationToken);
                }

                if (detectedType is null)
                {
                    throw new InvalidStoredFileException(
                        "Tipul fișierului nu este permis sau fișierul este invalid.");
                }

                var finalPath = GetFullPath(storageKey);

                Directory.CreateDirectory(
                    Path.GetDirectoryName(finalPath)!);

                File.Move(temporaryPath, finalPath);

                var sha256 = Convert.ToHexString(
                        hash.GetHashAndReset())
                    .ToLowerInvariant();

                return new StoredFile(
                    storageKey,
                    detectedType.ContentType,
                    size,
                    sha256);
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

        public FileStream OpenRead(string storageKey)
        {
            var path = GetFullPath(storageKey);

            return new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                options: FileOptions.Asynchronous |
                         FileOptions.SequentialScan);
        }
    }
}
