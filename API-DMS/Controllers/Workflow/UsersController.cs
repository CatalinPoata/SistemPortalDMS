using API_DMS.Data;
using API_DMS.DTO.Users;
using API_DMS.Entities.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_DMS.Controllers.Workflow
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = "Clerk,Admin")]
    public sealed class UsersController : ControllerBase
    {
        private readonly DmsDbContext db;

        public UsersController(DmsDbContext db)
        {
            this.db = db;
        }

        [HttpGet("assignees")]
        public async Task<ActionResult<IReadOnlyList<AssigneeResponse>>>
            GetAssignees(CancellationToken cancellationToken)
        {
            var users = await db.Users
                .AsNoTracking()
                .Where(user =>
                    user.is_active &&
                    (user.role == Role.Clerk ||
                     user.role == Role.Admin))
                .OrderBy(user => user.full_name)
                .ThenBy(user => user.email)
                .Select(user => new AssigneeResponse(
                    user.id,
                    user.full_name,
                    user.email,
                    user.role))
                .ToListAsync(cancellationToken);

            return Ok(users);
        }
    }
}
