using API_PORTAL.Data;
using API_PORTAL.DTO.PublicServices;
using API_PORTAL.DTO.ServiceDefinitions;
using API_PORTAL.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_PORTAL.Controllers.PublicServices
{
    [ApiController]
    [Route("api/public/services")]
    [AllowAnonymous]
    public sealed class PublicServicesController : ControllerBase
    {
        private readonly PortalDbContext db;

        public PublicServicesController(PortalDbContext db)
        {
            this.db = db;
        }

        [HttpGet]
        public async Task<ActionResult<
            PagedResponse<PublicServiceListItemResponse>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 24,
            CancellationToken cancellationToken = default)
        {
            if (page < 1 || pageSize is < 1 or > 100)
            {
                return InvalidPage(page, pageSize);
            }

            var query = db.ServiceDefinitions
                .AsNoTracking()
                .Where(service => service.is_published);

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(service => service.display_order)
                .ThenBy(service => service.title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(service => new PublicServiceListItemResponse(
                    service.id,
                    service.code,
                    service.title,
                    service.short_description,
                    service.requires_attachment,
                    service.max_attachments))
                .ToListAsync(cancellationToken);

            return Ok(new PagedResponse<PublicServiceListItemResponse>(
                items,
                page,
                pageSize,
                total));
        }

        [HttpGet("{code}")]
        public async Task<ActionResult<PublicServiceDetailsResponse>>
            GetByCode(
                string code,
                CancellationToken cancellationToken)
        {
            var service = await db.ServiceDefinitions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.is_published && item.code == code,
                    cancellationToken);

            if (service is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Serviciu indisponibil",
                    "Serviciul nu există sau nu este publicat."));
            }

            return Ok(new PublicServiceDetailsResponse(
                service.id,
                service.code,
                service.title,
                service.short_description,
                service.description,
                service.form_schema.RootElement.Clone(),
                service.schema_version,
                service.requires_attachment,
                service.max_attachments));
        }

        private ActionResult InvalidPage(int page, int pageSize)
        {
            var errors = new Dictionary<string, string[]>();

            if (page < 1)
            {
                errors["page"] = ["Pagina trebuie să fie cel puțin 1."];
            }

            if (pageSize is < 1 or > 100)
            {
                errors["pageSize"] =
                [
                    "pageSize trebuie să fie între 1 și 100."
                ];
            }

            return UnprocessableEntity(
                ApiProblemDetails.CreateValidation(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Paginare invalidă",
                    "Parametrii de paginare sunt invalizi.",
                    errors));
        }
    }
}
