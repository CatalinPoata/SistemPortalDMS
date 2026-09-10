using API_DMS.Data;
using API_DMS.DTO.DocumentKinds;
using API_DMS.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace API_DMS.Controllers.Workflow
{
    [ApiController]
    [Route("api/document-kinds")]
    [Authorize(Roles = "Clerk,Admin")]
    public class DocumentKindsController : ControllerBase
    {
        private readonly DmsDbContext db;

        public DocumentKindsController(DmsDbContext db)
        {
            this.db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentKindResponse>>> GetAll(
            [FromQuery] bool includeInactive = false)
        {
            var query = db.DocumentKinds
                .AsNoTracking()
                .AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(x => x.is_active);
            }

            var result = await query
                .OrderBy(x => x.code)
                .Select(x => new DocumentKindResponse(
                    x.id,
                    x.code,
                    x.name,
                    x.is_active))
                .ToListAsync();

            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DocumentKindResponse>> GetById(Guid id)
        {
            var result = await db.DocumentKinds
                .AsNoTracking()
                .Where(x => x.id == id)
                .Select(x => new DocumentKindResponse(
                    x.id,
                    x.code,
                    x.name,
                    x.is_active))
                .SingleOrDefaultAsync();

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<DocumentKindResponse>> Create(
            CreateDocumentKindRequest request)
        {
            var code = request.Code.Trim().ToUpperInvariant();
            var name = request.Name.Trim();

            if (string.IsNullOrWhiteSpace(code) ||
                string.IsNullOrWhiteSpace(name))
            {
                return UnprocessableEntity(new
                {
                    error = "Codul și numele sunt obligatorii."
                });
            }

            var exists = await db.DocumentKinds
                .AnyAsync(x => x.code == code);

            if (exists)
            {
                return Conflict(new
                {
                    error = "Există deja un tip de document cu acest cod."
                });
            }

            var documentKind = new DocumentKind
            {
                id = Guid.NewGuid(),
                code = code,
                name = name,
                is_active = true
            };

            db.DocumentKinds.Add(documentKind);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException postgres &&
                      postgres.SqlState == "23505")
            {
                return Conflict(new
                {
                    error = "Codul tipului de document există deja."
                });
            }

            return CreatedAtAction(
                nameof(GetById),
                new { id = documentKind.id },
                ToResponse(documentKind));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<DocumentKindResponse>> Update(
            Guid id,
            UpdateDocumentKindRequest request)
        {
            var documentKind = await db.DocumentKinds
                .SingleOrDefaultAsync(x => x.id == id);

            if (documentKind is null)
            {
                return NotFound();
            }

            var code = request.Code.Trim().ToUpperInvariant();
            var name = request.Name.Trim();

            var codeExists = await db.DocumentKinds
                .AnyAsync(x => x.id != id && x.code == code);

            if (codeExists)
            {
                return Conflict(new
                {
                    error = "Există deja un tip de document cu acest cod."
                });
            }

            documentKind.code = code;
            documentKind.name = name;
            documentKind.is_active = request.IsActive;

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException postgres &&
                      postgres.SqlState == "23505")
            {
                return Conflict(new
                {
                    error = "Codul tipului de document există deja."
                });
            }

            return Ok(ToResponse(documentKind));
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var documentKind = await db.DocumentKinds
                .SingleOrDefaultAsync(x => x.id == id);

            if (documentKind is null)
            {
                return NotFound();
            }

            var isUsed = await db.RegistryDocuments
                .AnyAsync(x => x.document_kind_id == id);

            if (isUsed)
            {
                documentKind.is_active = false;
                await db.SaveChangesAsync();

                return NoContent();
            }

            db.DocumentKinds.Remove(documentKind);
            await db.SaveChangesAsync();

            return NoContent();
        }

        private static DocumentKindResponse ToResponse(
            DocumentKind documentKind)
        {
            return new DocumentKindResponse(
                documentKind.id,
                documentKind.code,
                documentKind.name,
                documentKind.is_active);
        }
    }
}
