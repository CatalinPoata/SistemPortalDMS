using System.IO.Compression;
using System.Text;

namespace API_PORTAL.Storage
{
    public sealed record DetectedSubmissionFileType(string ContentType);

    public sealed class SubmissionFileContentDetector
    {
        public async Task<DetectedSubmissionFileType?> DetectAsync(
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
                header.AsMemory(),
                cancellationToken);

            stream.Position = 0;

            if (bytesRead >= 5 &&
                Encoding.ASCII.GetString(header, 0, 5) == "%PDF-")
            {
                return new("application/pdf");
            }

            if (bytesRead >= 8 &&
                header.AsSpan(0, 8).SequenceEqual(
                    new byte[]
                    {
                        0x89, 0x50, 0x4E, 0x47,
                        0x0D, 0x0A, 0x1A, 0x0A
                    }))
            {
                return new("image/png");
            }

            if (bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF)
            {
                return new("image/jpeg");
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

        private static async Task<DetectedSubmissionFileType?>
            DetectZipDocumentAsync(
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
                        return new(mimeType);
                    }
                }

                if (archive.GetEntry("[Content_Types].xml") is not null &&
                    archive.GetEntry("word/document.xml") is not null)
                {
                    return new(
                        "application/vnd.openxmlformats-officedocument." +
                        "wordprocessingml.document");
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
