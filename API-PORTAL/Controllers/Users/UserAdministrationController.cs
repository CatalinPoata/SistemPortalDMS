using API_PORTAL.Data;
using API_PORTAL.DTO.ServiceDefinitions;
using API_PORTAL.DTO.Users;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace API_PORTAL.Controllers.Users
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = nameof(Role.Admin))]
    public sealed class UserAdministrationController : ControllerBase
    {
        private const int MaximumPageSize = 100;

        private readonly PortalDbContext db;

        public UserAdministrationController(PortalDbContext db)
        {
            this.db = db;
        }

        [HttpGet]
        public async Task<ActionResult<
            PagedResponse<PortalUserAdministrationResponse>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? search = null,
            CancellationToken cancellationToken = default)
        {
            if (page < 1 || pageSize is < 1 or > MaximumPageSize)
            {
                return UnprocessableEntity(ApiProblemDetails.CreateValidation(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Paginare invalidă",
                    "page trebuie să fie cel puțin 1, iar pageSize între 1 și 100.",
                    new Dictionary<string, string[]>
                    {
                        [page < 1 ? "page" : "pageSize"] =
                        ["Valoarea este în afara intervalului permis."]
                    }));
            }

            var term = search?.Trim().ToLowerInvariant();
            var query = db.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(term))
            {
                query = query.Where(user =>
                    user.email.ToLower().Contains(term) ||
                    user.full_name.ToLower().Contains(term));
            }

            var total = await query.CountAsync(cancellationToken);
            var users = await query
                .OrderBy(user => user.full_name)
                .ThenBy(user => user.email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(user => ToResponse(user))
                .ToListAsync(cancellationToken);

            return Ok(new PagedResponse<PortalUserAdministrationResponse>(
                users,
                page,
                pageSize,
                total));
        }

        [HttpPut("{id:guid}/active")]
        public async Task<ActionResult<PortalUserAdministrationResponse>>
            SetActive(
                Guid id,
                UpdateUserActiveRequest request,
                CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId is null)
            {
                return Unauthorized();
            }

            var user = await db.Users.SingleOrDefaultAsync(
                item => item.id == id,
                cancellationToken);
            if (user is null)
            {
                return NotFound(UserNotFound());
            }

            if (!request.IsActive && user.id == currentUserId.Value)
            {
                return CannotChangeOwnAdministration(
                    "Nu îți poți dezactiva propriul cont de administrator.");
            }

            if (!request.IsActive && user.is_active && user.role == Role.Admin &&
                !await HasAnotherActiveAdministratorAsync(
                    user.id,
                    cancellationToken))
            {
                return CannotChangeOwnAdministration(
                    "Trebuie să existe cel puțin un administrator activ.");
            }

            if (user.is_active != request.IsActive)
            {
                user.is_active = request.IsActive;
                user.failed_login_count = 0;
                user.lockout_end = null;
                await RevokeSessionsAsync(user.id, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }

            return Ok(ToResponse(user));
        }

        [HttpPut("{id:guid}/role")]
        public async Task<ActionResult<PortalUserAdministrationResponse>>
            SetRole(
                Guid id,
                UpdateUserRoleRequest request,
                CancellationToken cancellationToken)
        {
            if (request.Role is not (Role.Citizen or Role.Admin))
            {
                return UnprocessableEntity(ApiProblemDetails.CreateValidation(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Rol invalid",
                    "În Portal sunt permise doar rolurile Citizen și Admin.",
                    new Dictionary<string, string[]>
                    {
                        ["role"] = ["Rolul trebuie să fie Citizen sau Admin."]
                    }));
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId is null)
            {
                return Unauthorized();
            }

            var user = await db.Users.SingleOrDefaultAsync(
                item => item.id == id,
                cancellationToken);
            if (user is null)
            {
                return NotFound(UserNotFound());
            }

            if (user.role == Role.Admin && request.Role != Role.Admin)
            {
                if (user.id == currentUserId.Value)
                {
                    return CannotChangeOwnAdministration(
                        "Nu îți poți retrage propriul rol de administrator.");
                }

                if (user.is_active && !await HasAnotherActiveAdministratorAsync(
                        user.id,
                        cancellationToken))
                {
                    return CannotChangeOwnAdministration(
                        "Trebuie să existe cel puțin un administrator activ.");
                }
            }

            if (user.role != request.Role.Value)
            {
                user.role = request.Role.Value;
                await RevokeSessionsAsync(user.id, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }

            return Ok(ToResponse(user));
        }

        private async Task<bool> HasAnotherActiveAdministratorAsync(
            Guid excludedUserId,
            CancellationToken cancellationToken)
        {
            return await db.Users.AnyAsync(user =>
                user.id != excludedUserId &&
                user.is_active &&
                user.role == Role.Admin,
                cancellationToken);
        }

        private async Task RevokeSessionsAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var tokens = await db.RefreshTokens
                .Where(token =>
                    token.user_id == userId &&
                    token.revoked_at == null)
                .ToListAsync(cancellationToken);

            foreach (var token in tokens)
            {
                token.revoked_at = now;
            }
        }

        private Guid? GetCurrentUserId()
        {
            var value = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(value, out var id) ? id : null;
        }

        private ProblemDetails UserNotFound()
        {
            return ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                "Utilizator inexistent",
                "Utilizatorul solicitat nu există.");
        }

        private ActionResult CannotChangeOwnAdministration(string detail)
        {
            return UnprocessableEntity(ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Modificare nepermisă",
                detail));
        }

        private static PortalUserAdministrationResponse ToResponse(User user)
        {
            return new PortalUserAdministrationResponse(
                user.id,
                user.email,
                user.full_name,
                user.role,
                user.email_confirmed,
                user.is_active,
                user.created_at,
                user.updated_at);
        }
    }
}
