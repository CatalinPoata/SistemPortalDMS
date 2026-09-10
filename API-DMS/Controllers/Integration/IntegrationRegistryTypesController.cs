using API_DMS.Data;
using API_DMS.DTO.Integration;
using API_DMS.Errors;
using API_DMS.Integration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_DMS.Controllers.Integration
{
    [ApiController]
    [Route("api/integration/registry-types")]
    [ServiceAuthentication]
    public sealed class IntegrationRegistryTypesController
        : ControllerBase
    {
        private readonly DmsDbContext db;

        public IntegrationRegistryTypesController(DmsDbContext db)
        {
            this.db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<
            IntegrationRegistryTypeResponse>>> GetAll(
            CancellationToken cancellationToken)
        {
            var registries = await db.RegistryTypes
                .AsNoTracking()
                .OrderBy(item => item.code)
                .Select(item => new IntegrationRegistryTypeResponse(
                    item.id,
                    item.code,
                    item.name,
                    item.direction,
                    item.is_closed))
                .ToListAsync(cancellationToken);

            return Ok(registries);
        }

        [HttpGet("{code}")]
        public async Task<ActionResult<IntegrationRegistryTypeResponse>>
            GetByCode(
                string code,
                CancellationToken cancellationToken)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();

            var registry = await db.RegistryTypes
                .AsNoTracking()
                .Where(item => item.code == normalizedCode)
                .Select(item => new IntegrationRegistryTypeResponse(
                    item.id,
                    item.code,
                    item.name,
                    item.direction,
                    item.is_closed))
                .SingleOrDefaultAsync(cancellationToken);

            if (registry is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Registru inexistent",
                    "Codul de registru nu există în DMS."));
            }

            return Ok(registry);
        }
    }
}
