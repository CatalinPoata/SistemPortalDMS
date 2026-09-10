using API_PORTAL.Auth;
using API_PORTAL.Data;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL_TESTS.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace API_PORTAL_TESTS.Tests
{
    public sealed class UserAdministrationHttpTests
        : IClassFixture<PortalApiFactory>
    {
        private readonly PortalApiFactory factory;

        public UserAdministrationHttpTests(PortalApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task B15_Admin_can_list_activate_and_change_another_users_role()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var (adminClient, admin) = await CreateClientAsync(
                Role.Admin,
                cancellationToken);
            var citizen = await SeedUserAsync(
                Role.Citizen,
                isActive: true,
                cancellationToken);

            using var list = await adminClient.GetAsync(
                "/api/users?page=1&pageSize=100",
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var listed = await list.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            Assert.Contains(listed.GetProperty("items").EnumerateArray(), item =>
                item.GetProperty("id").GetGuid() == citizen.id);

            using var promoted = await adminClient.PutAsJsonAsync(
                $"/api/users/{citizen.id}/role",
                new { role = "Admin" },
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
            var promotedBody = await promoted.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            Assert.Equal("Admin", promotedBody.GetProperty("role").GetString());

            using var deactivated = await adminClient.PutAsJsonAsync(
                $"/api/users/{citizen.id}/active",
                new { isActive = false },
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
            var deactivatedBody = await deactivated.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            Assert.False(deactivatedBody.GetProperty("isActive").GetBoolean());

            using var ownDeactivation = await adminClient.PutAsJsonAsync(
                $"/api/users/{admin.id}/active",
                new { isActive = false },
                cancellationToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity,
                ownDeactivation.StatusCode);

            using var invalidRole = await adminClient.PutAsJsonAsync(
                $"/api/users/{citizen.id}/role",
                new { role = "Clerk" },
                cancellationToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity,
                invalidRole.StatusCode);
        }

        [Fact]
        public async Task Citizen_cannot_administer_portal_users()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var (client, _) = await CreateClientAsync(
                Role.Citizen,
                cancellationToken);

            using var response = await client.GetAsync(
                "/api/users",
                cancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        private async Task<(HttpClient Client, User User)> CreateClientAsync(
            Role role,
            CancellationToken cancellationToken)
        {
            var user = await SeedUserAsync(role, true, cancellationToken);
            await using var scope = factory.Services.CreateAsyncScope();
            var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false
            });
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    tokens.Create(user).AccessToken);
            return (client, user);
        }

        private async Task<User> SeedUserAsync(
            Role role,
            bool isActive,
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var user = new User
            {
                id = Guid.NewGuid(),
                email = $"users-{Guid.NewGuid():N}@example.com",
                full_name = "Utilizator administrat",
                role = role,
                password_hash = "unused",
                email_confirmed = true,
                is_active = isActive
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return user;
        }
    }
}
