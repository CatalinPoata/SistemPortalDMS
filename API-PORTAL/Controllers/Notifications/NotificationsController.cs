using API_PORTAL.Data;
using API_PORTAL.DTO.Notifications;
using API_PORTAL.Entities.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace API_PORTAL.Controllers.Notifications;

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = nameof(Role.Citizen))]
public sealed class NotificationsController : ControllerBase
{
    private const int MaxPageSize = 100;
    private readonly PortalDbContext db;

    public NotificationsController(PortalDbContext db)
    {
        this.db = db;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationListResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Notifications
            .AsNoTracking()
            .Where(item => item.user_id == userId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.read_at != null)
            .ThenByDescending(item => item.created_at)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new NotificationResponse(
                item.id,
                item.subject,
                item.body,
                item.link_url,
                item.read_at != null,
                item.created_at))
            .ToListAsync(cancellationToken);

        return Ok(new NotificationListResponse(items, page, pageSize, total));
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Forbid();

        var notification = await db.Notifications.SingleOrDefaultAsync(
            item => item.id == notificationId && item.user_id == userId,
            cancellationToken);

        if (notification is null) return NotFound();

        notification.read_at ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Forbid();

        await db.Notifications
            .Where(item => item.user_id == userId && item.read_at == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.read_at,
                    DateTimeOffset.UtcNow),
                cancellationToken);

        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            out userId);
    }
}
