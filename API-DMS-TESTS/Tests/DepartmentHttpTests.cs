using API_DMS.Auth;
using API_DMS.Data;
using API_DMS.Entities;
using API_DMS.Entities.Base;
using API_DMS_TESTS.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace API_DMS_TESTS.Tests
{
    public sealed class DepartmentHttpTests : IClassFixture<DmsApiFactory>
    {
        private readonly DmsApiFactory factory;

        public DepartmentHttpTests(DmsApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task D11_Admin_can_create_update_and_deactivate_department()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var client = await CreateAdminClientAsync(cancellationToken);
            var code = $"D{Guid.NewGuid():N}"[..20].ToUpperInvariant();

            using var created = await client.PostAsJsonAsync(
                "/api/departments",
                new { code, name = "Compartiment test" },
                cancellationToken);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var createBody = await created.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            var id = createBody.GetProperty("id").GetGuid();
            Assert.True(createBody.GetProperty("isActive").GetBoolean());

            using var updated = await client.PutAsJsonAsync(
                $"/api/departments/{id}",
                new
                {
                    code,
                    name = "Compartiment actualizat",
                    managerUserId = (Guid?)null,
                    isActive = false
                },
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            var updateBody = await updated.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            Assert.Equal("Compartiment actualizat", updateBody.GetProperty("name").GetString());
            Assert.False(updateBody.GetProperty("isActive").GetBoolean());

            using var list = await client.GetAsync(
                "/api/departments?includeInactive=true",
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var departments = await list.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            Assert.Contains(departments.EnumerateArray(), item =>
                item.GetProperty("id").GetGuid() == id);
        }

        private async Task<HttpClient> CreateAdminClientAsync(
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
            var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var admin = new User
            {
                id = Guid.NewGuid(),
                email = $"departments-{Guid.NewGuid():N}@example.com",
                full_name = "Administrator compartimente",
                password_hash = "unused",
                role = Role.Admin,
                email_confirmed = true,
                is_active = true
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync(cancellationToken);

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false
            });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                tokens.Create(admin).AccessToken);
            return client;
        }
    }
}
