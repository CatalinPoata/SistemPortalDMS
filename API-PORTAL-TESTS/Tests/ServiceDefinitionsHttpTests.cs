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
using Task = System.Threading.Tasks.Task;

namespace API_PORTAL_TESTS.Tests
{
    public sealed class ServiceDefinitionsHttpTests
        : IClassFixture<PortalApiFactory>
    {
        private readonly PortalApiFactory factory;

        public ServiceDefinitionsHttpTests(PortalApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task Admin_can_create_service_and_html_is_sanitized()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var client = await CreateAdminClientAsync(cancellationToken);
            var code = $"test-{Guid.NewGuid():N}";

            using var response = await client.PostAsJsonAsync(
                "/api/service-definitions",
                CreateRequest(
                    code,
                    ValidSchema(),
                    "<script>alert(1)</script><p>Descriere sigură</p>"),
                cancellationToken);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var created = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);

            Assert.Equal(code, created.GetProperty("code").GetString());
            Assert.Equal(1, created.GetProperty("schemaVersion").GetInt32());

            var description = created.GetProperty("description").GetString();
            Assert.DoesNotContain("script", description, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Descriere sigură", description);
        }

        [Fact]
        public async Task Admin_can_list_dms_registry_types_including_closed_ones()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            factory.DmsRegistryTypes.SetRegistry(
                "IESIRI",
                isClosed: true,
                name: "Registru de ieșiri",
                direction: "Out");
            var client = await CreateAdminClientAsync(cancellationToken);

            using var response = await client.GetAsync(
                "/api/service-definitions/dms-registry-types",
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var registries = await response.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);

            var closed = registries.EnumerateArray().Single(item =>
                item.GetProperty("code").GetString() == "IESIRI");

            Assert.Equal(
                "Registru de ieșiri",
                closed.GetProperty("name").GetString());
            Assert.Equal("Out", closed.GetProperty("direction").GetString());
            Assert.True(closed.GetProperty("isClosed").GetBoolean());
        }

        [Fact]
        public async Task Publishing_invalid_schema_returns_422_and_keeps_service_unpublished()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var client = await CreateAdminClientAsync(cancellationToken);
            var code = $"draft-{Guid.NewGuid():N}";

            using (var createResponse = await client.PostAsJsonAsync(
                "/api/service-definitions",
                CreateRequest(code, new { sections = Array.Empty<object>() }),
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            }

            using var publishResponse = await client.PostAsync(
                $"/api/service-definitions/{code}/publish",
                content: null,
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.UnprocessableEntity,
                publishResponse.StatusCode);

            var problem = await publishResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);

            Assert.True(problem.GetProperty("errors")
                .TryGetProperty("formSchema.sections", out _));

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var service = await db.ServiceDefinitions.SingleAsync(
                item => item.code == code,
                cancellationToken);

            Assert.False(service.is_published);
        }

        [Fact]
        public async Task Publishing_schema_without_required_flag_returns_422()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var client = await CreateAdminClientAsync(cancellationToken);
            var code = $"required-{Guid.NewGuid():N}";
            var schema = new
            {
                sections = new[]
                {
                    new
                    {
                        key = "solicitant",
                        title = "Date solicitant",
                        fields = new[]
                        {
                            new
                            {
                                key = "nume",
                                label = "Nume și prenume",
                                type = "text",
                                maxLength = 200
                            }
                        }
                    }
                }
            };

            using (var createResponse = await client.PostAsJsonAsync(
                "/api/service-definitions",
                CreateRequest(code, schema),
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            }

            using var publishResponse = await client.PostAsync(
                $"/api/service-definitions/{code}/publish",
                content: null,
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.UnprocessableEntity,
                publishResponse.StatusCode);

            var problem = await publishResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);

            Assert.True(problem.GetProperty("errors")
                .TryGetProperty(
                    "formSchema.sections[0].fields[0].required",
                    out _));
        }

        [Fact]
        public async Task Publishing_with_unknown_dms_registry_returns_422()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var client = await CreateAdminClientAsync(cancellationToken);
            var code = $"dms-registry-{Guid.NewGuid():N}";

            using (var createResponse = await client.PostAsJsonAsync(
                "/api/service-definitions",
                CreateRequest(
                    code,
                    ValidSchema(),
                    registryTypeCode: "LIPSESTE"),
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            }

            using var publishResponse = await client.PostAsync(
                $"/api/service-definitions/{code}/publish",
                content: null,
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.UnprocessableEntity,
                publishResponse.StatusCode);

            var problem = await publishResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);

            Assert.True(problem.GetProperty("errors")
                .TryGetProperty("registryTypeCode", out _));
        }

        [Fact]
        public async Task Changing_schema_increments_version()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var client = await CreateAdminClientAsync(cancellationToken);
            var code = $"version-{Guid.NewGuid():N}";

            using (var createResponse = await client.PostAsJsonAsync(
                "/api/service-definitions",
                CreateRequest(code, ValidSchema()),
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            }

            using var updateResponse = await client.PutAsJsonAsync(
                $"/api/service-definitions/{code}",
                new
                {
                    title = "Serviciu actualizat",
                    shortDescription = "Descriere scurtă",
                    description = "<p>Conținut</p>",
                    registryTypeCode = "INTRARI",
                    formSchema = ValidSchema(maxLength: 120),
                    requiresAttachment = false,
                    maxAttachments = 3,
                    displayOrder = 2,
                    expectedSchemaVersion = 1
                },
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            var updated = await updateResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);

            Assert.Equal(2, updated.GetProperty("schemaVersion").GetInt32());
        }

        [Fact]
        public async Task Citizen_cannot_create_service()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var client = await CreateClientAsync(
                Role.Citizen,
                cancellationToken);

            using var response = await client.PostAsJsonAsync(
                "/api/service-definitions",
                CreateRequest($"forbidden-{Guid.NewGuid():N}", ValidSchema()),
                cancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);
        }

        private async Task<HttpClient> CreateAdminClientAsync(
            CancellationToken cancellationToken)
        {
            return await CreateClientAsync(Role.Admin, cancellationToken);
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
                email = $"service-test-{Guid.NewGuid():N}@example.com",
                full_name = "Service Test",
                role = role,
                email_confirmed = true,
                is_active = true,
                password_hash = "unused"
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);

            var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    tokenService.Create(user).AccessToken);

            return client;
        }

        private static object CreateRequest(
            string code,
            object formSchema,
            string? description = null,
            string registryTypeCode = "INTRARI")
        {
            return new
            {
                code,
                title = "Serviciu de test",
                shortDescription = "Descriere de test",
                description,
                registryTypeCode,
                formSchema,
                requiresAttachment = false,
                maxAttachments = 3,
                displayOrder = 0
            };
        }

        private static object ValidSchema(int maxLength = 200)
        {
            return new
            {
                sections = new[]
                {
                    new
                    {
                        key = "solicitant",
                        title = "Date solicitant",
                        fields = new[]
                        {
                            new
                            {
                                key = "nume",
                                label = "Nume și prenume",
                                type = "text",
                                required = true,
                                maxLength
                            }
                        }
                    }
                }
            };
        }
    }
}
