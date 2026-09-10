using API_PORTAL.Data;
using API_PORTAL.Entities;
using API_PORTAL.Errors;
using API_PORTAL.Integration;
using API_PORTAL.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_PORTAL.Controllers.Integration
{
    [ApiController]
    [Route("api/internal/files")]
    [ServiceAuthentication]
    public sealed class InternalSubmissionFilesController : ControllerBase
    {
        private readonly PortalDbContext db;
        private readonly SubmissionFileStorageService storage;

        public InternalSubmissionFilesController(
            PortalDbContext db,
            SubmissionFileStorageService storage)
        {
            this.db = db;
            this.storage = storage;
        }

        [HttpGet("{fileId:guid}/content")]
        public async Task<IActionResult> Download(
            Guid fileId,
            CancellationToken cancellationToken)
        {
            var file = await db.SubmissionFiles
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.id == fileId,
                    cancellationToken);

            if (file is null || file.kind == SubmissionFileKind.Response)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Fișier inexistent",
                    "Fișierul nu există sau nu poate fi transferat."));
            }

            try
            {
                return File(
                    storage.OpenRead(file.storage_key),
                    file.content_type,
                    file.original_name,
                    enableRangeProcessing: false);
            }
            catch (FileNotFoundException)
            {
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Fișier indisponibil",
                    detail: "Metadatele există, dar fișierul lipsește din storage.");
            }
        }
    }
}
