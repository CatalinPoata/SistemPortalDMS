using API_DMS.Data;
using API_DMS.DTO.Departments;
using API_DMS.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace API_DMS.Controllers.Workflow
{
    [ApiController]
    [Route("api/departments")]
    [Authorize(Roles = "Clerk,Admin")]
    public sealed class DepartmentsController : ControllerBase
    {
        private readonly DmsDbContext db;

        public DepartmentsController(DmsDbContext db)
        {
            this.db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DepartmentResponse>>> GetAll(
            [FromQuery] bool includeInactive = false,
            CancellationToken cancellationToken = default)
        {
            if (includeInactive && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var query = db.Departments
                .AsNoTracking()
                .AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(x => x.is_active);
            }

            var departments = await query
                .OrderBy(x => x.code)
                .Select(x => new DepartmentResponse(
                    x.id,
                    x.code,
                    x.name,
                    x.manager_user_id,
                    x.manager_user == null
                        ? null
                        : x.manager_user.email,
                    x.is_active))
                .ToListAsync(cancellationToken);

            return Ok(departments);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DepartmentResponse>> GetById(
            Guid id,
            CancellationToken cancellationToken)
        {
            var department = await db.Departments
                .AsNoTracking()
                .Where(x => x.id == id)
                .Select(x => new DepartmentResponse(
                    x.id,
                    x.code,
                    x.name,
                    x.manager_user_id,
                    x.manager_user == null
                        ? null
                        : x.manager_user.email,
                    x.is_active))
                .SingleOrDefaultAsync(cancellationToken);

            if (department is null)
            {
                return NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Compartiment inexistent",
                    "Compartimentul nu există."));
            }

            return Ok(department);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<DepartmentResponse>> Create(
            CreateDepartmentRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return UnprocessableEntity(CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Compartiment invalid",
                    "Datele compartimentului sunt obligatorii."));
            }

            var code = request.Code?.Trim().ToUpperInvariant();
            var name = request.Name?.Trim();

            if (string.IsNullOrWhiteSpace(code) ||
                code.Length > 20)
            {
                return UnprocessableEntity(CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Cod invalid",
                    "Codul este obligatoriu și nu poate depăși 20 de caractere."));
            }

            if (string.IsNullOrWhiteSpace(name) ||
                name.Length > 200)
            {
                return UnprocessableEntity(CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Denumire invalidă",
                    "Denumirea este obligatorie și nu poate depăși 200 de caractere."));
            }

            var codeExists = await db.Departments
                .AnyAsync(x => x.code == code, cancellationToken);

            if (codeExists)
            {
                return Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Cod duplicat",
                    "Există deja un compartiment cu acest cod."));
            }

            User? manager = null;

            if (request.ManagerUserId.HasValue)
            {
                manager = await db.Users
                    .SingleOrDefaultAsync(
                        x => x.id == request.ManagerUserId.Value &&
                             x.is_active,
                        cancellationToken);

                if (manager is null)
                {
                    return UnprocessableEntity(CreateProblem(
                        StatusCodes.Status422UnprocessableEntity,
                        "Manager invalid",
                        "Managerul nu există sau este inactiv."));
                }

                if (manager.role is not
                    (API_DMS.Entities.Base.Role.Clerk or
                     API_DMS.Entities.Base.Role.Admin))
                {
                    return UnprocessableEntity(CreateProblem(
                        StatusCodes.Status422UnprocessableEntity,
                        "Manager invalid",
                        "Managerul trebuie să aibă rolul Clerk sau Admin."));
                }
            }

            var department = new Department
            {
                id = Guid.NewGuid(),
                code = code,
                name = name,
                manager_user_id = manager?.id,
                is_active = true
            };

            db.Departments.Add(department);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException postgres &&
                      postgres.SqlState == "23505")
            {
                return Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Cod duplicat",
                    "Există deja un compartiment cu acest cod."));
            }

            return CreatedAtAction(
                nameof(GetById),
                new { id = department.id },
                new DepartmentResponse(
                    department.id,
                    department.code,
                    department.name,
                    department.manager_user_id,
                    manager?.email,
                    department.is_active));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<DepartmentResponse>> Update(
            Guid id,
            UpdateDepartmentRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return UnprocessableEntity(CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Compartiment invalid",
                    "Datele compartimentului sunt obligatorii."));
            }

            var department = await db.Departments
                .SingleOrDefaultAsync(
                    x => x.id == id,
                    cancellationToken);

            if (department is null)
            {
                return NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Compartiment inexistent",
                    "Compartimentul nu există."));
            }

            var code = request.Code?.Trim().ToUpperInvariant();
            var name = request.Name?.Trim();

            if (string.IsNullOrWhiteSpace(code) ||
                code.Length > 20)
            {
                return UnprocessableEntity(CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Cod invalid",
                    "Codul este obligatoriu și nu poate depăși 20 de caractere."));
            }

            if (string.IsNullOrWhiteSpace(name) ||
                name.Length > 200)
            {
                return UnprocessableEntity(CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Denumire invalidă",
                    "Denumirea este obligatorie și nu poate depăși 200 de caractere."));
            }

            var codeExists = await db.Departments
                .AnyAsync(
                    x => x.id != id &&
                         x.code == code,
                    cancellationToken);

            if (codeExists)
            {
                return Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Cod duplicat",
                    "Există deja un compartiment cu acest cod."));
            }

            User? manager = null;

            if (request.ManagerUserId.HasValue)
            {
                manager = await db.Users
                    .SingleOrDefaultAsync(
                        x => x.id == request.ManagerUserId.Value &&
                             x.is_active,
                        cancellationToken);

                if (manager is null)
                {
                    return UnprocessableEntity(CreateProblem(
                        StatusCodes.Status422UnprocessableEntity,
                        "Manager invalid",
                        "Managerul nu există sau este inactiv."));
                }

                if (manager.role is not
                    (API_DMS.Entities.Base.Role.Clerk or
                     API_DMS.Entities.Base.Role.Admin))
                {
                    return UnprocessableEntity(CreateProblem(
                        StatusCodes.Status422UnprocessableEntity,
                        "Manager invalid",
                        "Managerul trebuie să aibă rolul Clerk sau Admin."));
                }
            }

            department.code = code;
            department.name = name;
            department.manager_user_id = manager?.id;
            department.is_active = request.IsActive;

            await db.SaveChangesAsync(cancellationToken);

            return Ok(new DepartmentResponse(
                department.id,
                department.code,
                department.name,
                department.manager_user_id,
                manager?.email,
                department.is_active));
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(
            Guid id,
            CancellationToken cancellationToken)
        {
            var department = await db.Departments
                .SingleOrDefaultAsync(
                    x => x.id == id,
                    cancellationToken);

            if (department is null)
            {
                return NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Compartiment inexistent",
                    "Compartimentul nu există."));
            }

            department.is_active = false;

            await db.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        private ProblemDetails CreateProblem(
            int status,
            string title,
            string detail)
        {
            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            };

            problem.Extensions["traceId"] =
                HttpContext.TraceIdentifier;

            return problem;
        }
    }
}
