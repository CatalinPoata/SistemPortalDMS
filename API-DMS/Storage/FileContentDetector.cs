using System.IO.Compression;
using System.Text;

namespace API_DMS.Storage
{
    public sealed record DetectedFileType(string ContentType);

    public sealed class FileContentDetector
    {
        public async Task<DetectedFileType?> DetectAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            if (!stream.CanSeek)
            {
                throw new InvalidOperationException(
                    "Stream-ul trebuie să suporte Seek.");
            }

            stream.Position = 0;

            var header = new byte[8];
            var bytesRead = await stream.ReadAsync(
                header.AsMemory(0, header.Length),
                cancellationToken);

            stream.Position = 0;

            if (bytesRead >= 5 &&
                Encoding.ASCII.GetString(header, 0, 5) == "%PDF-")
            {
                return new DetectedFileType("application/pdf");
            }

            if (bytesRead >= 8 &&
                header[0] == 0x89 &&
                header[1] == 0x50 &&
                header[2] == 0x4E &&
                header[3] == 0x47 &&
                header[4] == 0x0D &&
                header[5] == 0x0A &&
                header[6] == 0x1A &&
                header[7] == 0x0A)
            {
                return new DetectedFileType("image/png");
            }

            if (bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF)
            {
                return new DetectedFileType("image/jpeg");
            }

            if (bytesRead >= 2 &&
                header[0] == 0x50 &&
                header[1] == 0x4B)
            {
                return await DetectZipDocumentAsync(
                    stream,
                    cancellationToken);
            }

            return null;
        }

        private static async Task<DetectedFileType?> DetectZipDocumentAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            stream.Position = 0;

            try
            {
                using var archive = new ZipArchive(
                    stream,
                    ZipArchiveMode.Read,
                    leaveOpen: true);

                var odtMimeEntry = archive.GetEntry("mimetype");

                if (odtMimeEntry is not null)
                {
                    using var mimeStream = odtMimeEntry.Open();
                    using var reader = new StreamReader(mimeStream);

                    var mimeType = (
                        await reader.ReadToEndAsync(cancellationToken))
                        .Trim();

                    if (mimeType ==
                        "application/vnd.oasis.opendocument.text")
                    {
                        return new DetectedFileType(
                            "application/vnd.oasis.opendocument.text");
                    }
                }

                var hasDocxContent =
                    archive.GetEntry("[Content_Types].xml") is not null &&
                    archive.GetEntry("word/document.xml") is not null;

                if (hasDocxContent)
                {
                    return new DetectedFileType(
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
                }
            }
            catch (InvalidDataException)
            {
                return null;
            }

            return null;
        }
    }
}
