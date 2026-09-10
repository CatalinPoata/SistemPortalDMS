using API_PORTAL.Auth;
using API_PORTAL.Data;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL_TESTS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace API_PORTAL_TESTS.Tests;

public sealed class NotificationsHttpTests : IClassFixture<PortalApiFactory>
{
    private readonly PortalApiFactory factory;

    public NotificationsHttpTests(PortalApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Citizen_sees_and_marks_only_own_notifications()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (user, ownNotificationId, otherNotificationId, accessToken) =
            await SeedAsync(cancellationToken);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var list = await client.GetFromJsonAsync<NotificationListResponse>(
            "/api/notifications",
            cancellationToken);

        Assert.NotNull(list);
        var notification = Assert.Single(list!.Items);
        Assert.Equal(ownNotificationId, notification.Id);
        Assert.False(notification.IsRead);

        using var markOwn = await client.PostAsync(
            $"/api/notifications/{ownNotificationId}/read",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, markOwn.StatusCode);

        using var markOther = await client.PostAsync(
            $"/api/notifications/{otherNotificationId}/read",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, markOther.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        Assert.NotNull(await db.Notifications.Where(item =>
            item.id == ownNotificationId && item.user_id == user.id)
            .Select(item => item.read_at)
            .SingleAsync(cancellationToken));
    }

    private async Task<(User User, Guid OwnNotificationId,
        Guid OtherNotificationId, string AccessToken)> SeedAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var user = new User
        {
            id = Guid.NewGuid(),
            email = $"citizen-{Guid.NewGuid():N}@example.com",
            full_name = "Cetățean test",
            role = Role.Citizen,
            password_hash = "unused",
            email_confirmed = true,
            is_active = true
        };
        var otherUser = new User
        {
            id = Guid.NewGuid(),
            email = $"other-{Guid.NewGuid():N}@example.com",
            full_name = "Alt cetățean",
            role = Role.Citizen,
            password_hash = "unused",
            email_confirmed = true,
            is_active = true
        };
        var own = NewNotification(user.id);
        var other = NewNotification(otherUser.id);

        db.AddRange(user, otherUser, own, other);
        await db.SaveChangesAsync(cancellationToken);

        return (user, own.id, other.id, tokens.Create(user).AccessToken);
    }

    private static Notification NewNotification(Guid userId) => new()
    {
        id = Guid.NewGuid(),
        user_id = userId,
        subject = "Actualizare cerere",
        body = "Cererea a fost actualizată.",
        link_url = "/cereri/test"
    };

    private sealed record NotificationResponse(Guid Id, bool IsRead);

    private sealed record NotificationListResponse(
        IReadOnlyList<NotificationResponse> Items,
        int Page,
        int PageSize,
        int Total);
}
