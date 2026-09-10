using API_DMS.Data;
using API_DMS.DTO.RegistryTypes;
using API_DMS.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace API_DMS.Controllers.Workflow
{
    [ApiController]
    [Route("api/registry-types")]
    [Authorize(Roles = "Clerk,Admin")]
    public class RegistryTypesController : ControllerBase
    {
        private readonly DmsDbContext db;

        public RegistryTypesController(DmsDbContext db)
        {
            this.db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RegistryTypeResponse>>> GetAll()
        {
            var registries = await db.RegistryTypes
                .AsNoTracking()
                .OrderBy(x => x.code)
                .Select(x => new RegistryTypeResponse(
                    x.id,
                    x.code,
                    x.name,
                    x.direction,
                    x.start_number,
                    x.default_deadline_days,
                    x.is_closed))
                .ToListAsync();

            return Ok(registries);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<RegistryTypeResponse>> GetById(Guid id)
        {
            var registry = await db.RegistryTypes
                .AsNoTracking()
                .Where(x => x.id == id)
                .Select(x => new RegistryTypeResponse(
                    x.id,
                    x.code,
                    x.name,
                    x.direction,
                    x.start_number,
                    x.default_deadline_days,
                    x.is_closed))
                .SingleOrDefaultAsync();

            if (registry is null)
            {
                return NotFound();
            }

            return Ok(registry);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<RegistryTypeResponse>> Create(
            CreateRegistryTypeRequest request)
        {
            var code = request.Code.Trim().ToUpperInvariant();

            if (!Enum.IsDefined(request.Direction))
            {
                return UnprocessableEntity(new
                {
                    error = "Direcția registrului este invalidă."
                });
            }

            var codeExists = await db.RegistryTypes
                .AnyAsync(x => x.code == code);

            if (codeExists)
            {
                return Conflict(new
                {
                    error = "Există deja un registru cu acest cod."
                });
            }

            var registry = new RegistryType
            {
                id = Guid.NewGuid(),
                code = code,
                name = request.Name.Trim(),
                direction = request.Direction,
                start_number = request.StartNumber,
                default_deadline_days = request.DefaultDeadlineDays,
                is_closed = false
            };

            db.RegistryTypes.Add(registry);

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
                    error = "Codul registrului există deja."
                });
            }

            return CreatedAtAction(
                nameof(GetById),
                new { id = registry.id },
                ToResponse(registry));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<RegistryTypeResponse>> Update(
            Guid id,
            UpdateRegistryTypeRequest request)
        {
            var registry = await db.RegistryTypes
                .SingleOrDefaultAsync(x => x.id == id);

            if (registry is null)
            {
                return NotFound();
            }

            var code = request.Code.Trim().ToUpperInvariant();

            var hasEntries = await db.RegistryEntries
                .AnyAsync(x => x.registry_type_id == id);

            if (hasEntries && registry.code != code)
            {
                return UnprocessableEntity(new
                {
                    error = "Codul nu mai poate fi modificat după prima înregistrare."
                });
            }

            var codeExists = await db.RegistryTypes
                .AnyAsync(x => x.id != id && x.code == code);

            if (codeExists)
            {
                return Conflict(new
                {
                    error = "Există deja un registru cu acest cod."
                });
            }

            if (!Enum.IsDefined(request.Direction))
            {
                return UnprocessableEntity(new
                {
                    error = "Direcția registrului este invalidă."
                });
            }

            if (hasEntries &&
                registry.direction != request.Direction)
            {
                return UnprocessableEntity(new
                {
                    error = "Direcția nu poate fi modificată după crearea pozițiilor."
                });
            }

            registry.code = code;
            registry.name = request.Name.Trim();
            registry.direction = request.Direction;
            registry.start_number = request.StartNumber;
            registry.default_deadline_days = request.DefaultDeadlineDays;
            registry.is_closed = request.IsClosed;

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
                    error = "Codul registrului există deja."
                });
            }

            return Ok(ToResponse(registry));
        }

        [HttpPost("{id:guid}/close")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<RegistryTypeResponse>> Close(Guid id)
        {
            var registry = await db.RegistryTypes
                .SingleOrDefaultAsync(x => x.id == id);

            if (registry is null)
            {
                return NotFound();
            }

            registry.is_closed = true;

            await db.SaveChangesAsync();

            return Ok(ToResponse(registry));
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var registry = await db.RegistryTypes
                .SingleOrDefaultAsync(x => x.id == id);

            if (registry is null)
            {
                return NotFound();
            }

            var hasEntries = await db.RegistryEntries
                .AnyAsync(x => x.registry_type_id == id);

            var hasCounters = await db.RegistryNumberCounters
                .AnyAsync(x => x.registry_type_id == id);

            if (hasEntries || hasCounters)
            {
                return Conflict(new
                {
                    error = "Registrul nu poate fi șters deoarece este deja utilizat."
                });
            }

            db.RegistryTypes.Remove(registry);
            await db.SaveChangesAsync();

            return NoContent();
        }

        private static RegistryTypeResponse ToResponse(RegistryType registry)
        {
            return new RegistryTypeResponse(
                registry.id,
                registry.code,
                registry.name,
                registry.direction,
                registry.start_number,
                registry.default_deadline_days,
                registry.is_closed);
        }
    }
}
