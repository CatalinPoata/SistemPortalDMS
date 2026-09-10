using API_DMS.Data;
using API_DMS.DTO.RegistryEntries;
using API_DMS.Errors;
using API_DMS.Integration;
using API_DMS.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API_DMS.Controllers.Integration
{
    [ApiController]
    [Route("api/internal/documents")]
    [ServiceAuthentication]
    public sealed class InternalDocumentsController : ControllerBase
    {
        private readonly DmsDbContext db;
        private readonly FileDownloadTokenService downloadTokens;
        private readonly FileDownloadOptions downloadOptions;

        public InternalDocumentsController(
            DmsDbContext db,
            FileDownloadTokenService downloadTokens,
            IOptions<FileDownloadOptions> downloadOptions)
        {
            this.db = db;
            this.downloadTokens = downloadTokens;
            this.downloadOptions = downloadOptions.Value;
        }

        [HttpPost("{documentId:guid}/download-url")]
        public async Task<ActionResult<SignedDownloadUrlResponse>>
            CreateDownloadUrl(
                Guid documentId,
                CancellationToken cancellationToken)
        {
            var document = await db.RegistryDocuments
                .AsNoTracking()
                .Include(item => item.entry)
                .SingleOrDefaultAsync(item =>
                    item.id == documentId &&
                    item.direction == Entities.DocumentDirection.Out &&
                    item.entry.external_id != null,
                    cancellationToken);

            if (document is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Document inexistent",
                    "Documentul de răspuns nu există pentru o cerere Portal."));
            }

            var signed = downloadTokens.Create(document.id);
            var baseUrl = string.IsNullOrWhiteSpace(
                downloadOptions.PublicBaseUrl)
                ? $"{Request.Scheme}://{Request.Host}"
                : downloadOptions.PublicBaseUrl.TrimEnd('/');
            var url = $"{baseUrl}/api/documents/{document.id}/content" +
                $"?token={Uri.EscapeDataString(signed.Token)}";

            return Ok(new SignedDownloadUrlResponse(
                document.id,
                url,
                signed.ExpiresAt));
        }
    }
}
