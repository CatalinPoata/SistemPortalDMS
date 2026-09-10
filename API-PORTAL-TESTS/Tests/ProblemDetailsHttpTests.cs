using API_PORTAL.Auth;
using API_PORTAL.Data;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL_TESTS.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace API_PORTAL_TESTS.Tests
{
    public sealed class ProblemDetailsHttpTests
    : IClassFixture<PortalApiFactory>
    {
        private const string TraceId =
            "11111111111111111111111111111111";

        private readonly HttpClient client;
        private readonly PortalApiFactory factory;

        public ProblemDetailsHttpTests(
            PortalApiFactory factory)
        {
            this.factory = factory;
            client = factory.CreateClient();
        }

        [Theory]
        [InlineData(null, null, 401)]
        [InlineData(Role.Citizen, Role.Citizen, 403)]
        [InlineData(Role.Admin, Role.Admin, 200)]
        [InlineData(Role.Admin, Role.Citizen, 403)]
        public async Task Admin_profile_enforces_role_and_problem_details(
            Role? tokenRole,
            Role? storedRole,
            int expectedStatus)
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var adminClient = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/auth/admin-me");

            request.Headers.Add("X-Trace-Id", TraceId);

            if (tokenRole.HasValue)
            {
                await using var scope = factory.Services.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<PortalDbContext>();

                var tokenService = scope.ServiceProvider
                    .GetRequiredService<IJwtTokenService>();

                var user = new User
                {
                    id = Guid.NewGuid(),
                    email = $"bo-{Guid.NewGuid():N}@example.com",
                    full_name = "Backoffice Test",
                    role = tokenRole.Value,
                    email_confirmed = true,
                    is_active = true,
                    password_hash = "unused-in-this-token-test"
                };

                var accessToken = tokenService.Create(user).AccessToken;

                user.role = storedRole ?? tokenRole.Value;

                db.Users.Add(user);
                await db.SaveChangesAsync(cancellationToken);

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
            }

            using var response = await adminClient.SendAsync(
                request,
                cancellationToken);

            Assert.Equal(expectedStatus, (int)response.StatusCode);

            if (expectedStatus == 200)
            {
                var profile = await response.Content
                    .ReadFromJsonAsync<JsonElement>(
                        cancellationToken: cancellationToken);

                Assert.Equal(
                    "Admin",
                    profile.GetProperty("role").GetString());

                return;
            }

            var problem = await ReadProblemAsync(response);

            Assert.Equal(expectedStatus, problem.Status);
            Assert.Equal("/api/auth/admin-me", problem.Instance);
            Assert.Equal(TraceId, problem.TraceId);

            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            Assert.Equal(
                TraceId,
                response.Headers.GetValues("X-Trace-Id").Single());

            Assert.False(string.IsNullOrWhiteSpace(problem.Type));
            Assert.False(string.IsNullOrWhiteSpace(problem.Title));
            Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
        }

        [Fact]
        public async Task Unauthenticated_request_returns_401_problem_details()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    "/api/auth/me");

            request.Headers.Add(
                "X-Trace-Id",
                TraceId);

            using var response =
                await client.SendAsync(request, cancellationToken);

            var problem =
                await ReadProblemAsync(response);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                response.StatusCode);

            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            Assert.Equal(
                TraceId,
                problem.TraceId);

            Assert.Equal(
                TraceId,
                response.Headers
                    .GetValues("X-Trace-Id")
                    .Single());

            Assert.NotNull(problem.Type);
            Assert.NotNull(problem.Title);
            Assert.NotNull(problem.Detail);
            Assert.NotNull(problem.Instance);
        }

        [Fact]
        public async Task Empty_login_body_returns_422_with_field_errors()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var content =
                new StringContent(
                    "{}",
                    Encoding.UTF8,
                    "application/json");

            using var response =
                await client.PostAsync(
                    "/api/auth/login",
                    content,
                    cancellationToken);

            var problem =
                await ReadProblemAsync(response);

            Assert.Equal(
                HttpStatusCode.UnprocessableEntity,
                response.StatusCode);

            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            Assert.NotNull(problem.Errors);
            Assert.Contains("Email", problem.Errors.Keys);
            Assert.Contains("Password", problem.Errors.Keys);
            Assert.NotNull(problem.TraceId);
        }

        [Theory]
        [InlineData("{", "$")]
        [InlineData("{\"email\":}", "$.email")]
        [InlineData("{\"email\":123}", "$.email")]
        [InlineData("{\"id\":null}", "$.id")]
        [InlineData("{\"created_at\":null}", "$.created_at")]
        public async Task Invalid_json_contract_returns_400_problem_details(
    string json,
    string errorKey)
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/auth/login");

            request.Headers.Add("X-Trace-Id", TraceId);

            request.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            using var response = await client.SendAsync(
                request,
                cancellationToken);

            var problem = await ReadProblemAsync(response);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(400, problem.Status);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            Assert.Equal("/api/auth/login", problem.Instance);
            Assert.Equal(TraceId, problem.TraceId);
            Assert.Equal(
                TraceId,
                response.Headers.GetValues("X-Trace-Id").Single());

            Assert.NotNull(problem.Errors);
            Assert.Contains(errorKey, problem.Errors.Keys);
        }

        [Fact]
        public async Task Unknown_route_returns_404_problem_details()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var response =
                await client.GetAsync(
                    "/api/route-that-does-not-exist",
                    cancellationToken);

            var problem =
                await ReadProblemAsync(response);

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);

            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            Assert.Equal(
                "/api/route-that-does-not-exist",
                problem.Instance);

            Assert.NotNull(problem.TraceId);
        }

        private static async Task<ProblemResponse>
            ReadProblemAsync(
                HttpResponseMessage response)
        {
            var problem =
                await response.Content
                    .ReadFromJsonAsync<ProblemResponse>();

            Assert.NotNull(problem);

            return problem!;
        }

        private sealed class ProblemResponse
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("status")]
            public int? Status { get; set; }

            [JsonPropertyName("detail")]
            public string? Detail { get; set; }

            [JsonPropertyName("instance")]
            public string? Instance { get; set; }

            [JsonPropertyName("traceId")]
            public string? TraceId { get; set; }

            [JsonPropertyName("errors")]
            public Dictionary<string, string[]>? Errors { get; set; }
        }
    }
}
