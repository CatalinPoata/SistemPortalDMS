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
    public sealed class ArticleHttpTests
        : IClassFixture<PortalApiFactory>
    {
        private readonly PortalApiFactory factory;

        public ArticleHttpTests(PortalApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task Admin_creates_sanitizes_and_publishes_article()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var client = await CreateAdminClientAsync(cancellationToken);

            using var createResponse = await client.PostAsJsonAsync(
                "/api/articles",
                new
                {
                    title = "Informații pentru cetățeni",
                    summary = "Noutăți utile.",
                    body = "<p>Informații <strong>utile</strong>.</p>" +
                        "<script>alert('x')</script>" +
                        "<img src=x onerror=alert(1)>"
                },
                cancellationToken);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var created = await createResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            var slug = created.GetProperty("slug").GetString();
            var body = created.GetProperty("body").GetString();

            Assert.Equal("informatii-pentru-cetateni", slug);
            Assert.NotNull(body);
            Assert.DoesNotContain("<script", body,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onerror", body,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<img", body,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<strong>utile</strong>", body,
                StringComparison.OrdinalIgnoreCase);

            using var publishResponse = await client.PostAsync(
                $"/api/articles/{slug}/publish",
                content: null,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

            var anonymous = CreateAnonymousClient();
            using var publicResponse = await anonymous.GetAsync(
                $"/api/public/articles/{slug}",
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);

            var published = await publicResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            Assert.Equal(slug, published.GetProperty("slug").GetString());
            Assert.DoesNotContain(
                "<script",
                published.GetProperty("body").GetString(),
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Public_catalog_excludes_drafts_and_future_articles()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var admin = await CreateAdminClientAsync(cancellationToken);
            var prefix = $"articol-{Guid.NewGuid():N}";

            var draft = await CreateArticleAsync(
                admin,
                $"{prefix}-draft",
                publishedAt: null,
                cancellationToken);
            var future = await CreateArticleAsync(
                admin,
                $"{prefix}-viitor",
                DateTimeOffset.UtcNow.AddDays(1),
                cancellationToken);
            var current = await CreateArticleAsync(
                admin,
                $"{prefix}-acum",
                DateTimeOffset.UtcNow,
                cancellationToken);

            await PublishAsync(admin, future, cancellationToken);
            await PublishAsync(admin, current, cancellationToken);

            var anonymous = CreateAnonymousClient();
            using var response = await anonymous.GetAsync(
                "/api/public/articles?page=1&pageSize=100",
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            var slugs = result.GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("slug").GetString())
                .ToArray();

            Assert.Contains(current, slugs);
            Assert.DoesNotContain(draft, slugs);
            Assert.DoesNotContain(future, slugs);

            using var hiddenResponse = await anonymous.GetAsync(
                $"/api/public/articles/{future}",
                cancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, hiddenResponse.StatusCode);
        }

        [Fact]
        public async Task Citizen_cannot_manage_articles()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var citizen = await CreateClientAsync(
                Role.Citizen,
                cancellationToken);

            using var response = await citizen.PostAsJsonAsync(
                "/api/articles",
                new
                {
                    title = "Articol nepermis",
                    body = "<p>Conținut</p>"
                },
                cancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        private async Task<string> CreateArticleAsync(
            HttpClient client,
            string slug,
            DateTimeOffset? publishedAt,
            CancellationToken cancellationToken)
        {
            using var response = await client.PostAsJsonAsync(
                "/api/articles",
                new
                {
                    slug,
                    title = $"Titlu {slug}",
                    body = "<p>Conținut sigur.</p>",
                    publishedAt
                },
                cancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            return (await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken))
                .GetProperty("slug")
                .GetString()!;
        }

        private static async Task PublishAsync(
            HttpClient client,
            string slug,
            CancellationToken cancellationToken)
        {
            using var response = await client.PostAsync(
                $"/api/articles/{slug}/publish",
                content: null,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private Task<HttpClient> CreateAdminClientAsync(
            CancellationToken cancellationToken)
        {
            return CreateClientAsync(Role.Admin, cancellationToken);
        }

        private async Task<HttpClient> CreateClientAsync(
            Role role,
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var tokenService = scope.ServiceProvider
                .GetRequiredService<IJwtTokenService>();
            var user = new User
            {
                id = Guid.NewGuid(),
                email = $"{role.ToString().ToLowerInvariant()}-" +
                    $"{Guid.NewGuid():N}@example.com",
                full_name = "Utilizator Test",
                role = role,
                email_confirmed = true,
                is_active = true,
                password_hash = "unused"
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);

            var client = CreateAnonymousClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    tokenService.Create(user).AccessToken);

            return client;
        }

        private HttpClient CreateAnonymousClient()
        {
            return factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });
        }
    }
}
