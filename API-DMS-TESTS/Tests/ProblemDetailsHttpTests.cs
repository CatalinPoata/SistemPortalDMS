using API_DMS.Data;
using API_DMS.Entities;
using API_DMS.Storage;
using API_DMS_TESTS.Infrastructure;
using Microsoft.EntityFrameworkCore;
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
using Task = System.Threading.Tasks.Task;
using Xunit;

namespace API_DMS_TESTS.Tests
{
    public sealed class ProblemDetailsHttpTests
    : IClassFixture<DmsApiFactory>
    {
        private const string TraceId =
            "11111111111111111111111111111111";

        private readonly HttpClient client;
        private readonly DmsApiFactory factory;

        public ProblemDetailsHttpTests(
            DmsApiFactory factory)
        {
            this.factory = factory;
            client = factory.CreateClient();
        }

        [Fact]
        public async Task Unauthenticated_request_returns_401_problem_details()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    "/api/registry-types");

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

        [Theory]
        [InlineData("Id")]
        [InlineData("storage_key")]
        [InlineData("SizeBytes")]
        public async Task Upload_with_server_field_returns_400(
            string fieldName)
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/registry-entries/{Guid.NewGuid()}/documents");

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                CreateAdminToken());

            var form = new MultipartFormDataContent();

            form.Add(
                new StringContent(Guid.NewGuid().ToString()),
                "DocumentKindId");

            form.Add(new StringContent("In"), "Direction");
            form.Add(new StringContent("valoare-nepermisa"), fieldName);

            form.Add(
                new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.7\n")),
                "File",
                "test.pdf");

            request.Content = form;

            using var response = await client.SendAsync(
                request,
                cancellationToken);

            var problem = await ReadProblemAsync(response);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(400, problem.Status);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            Assert.NotNull(problem.Errors);
            Assert.Contains(fieldName, problem.Errors.Keys);
            Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
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

        [Fact]
        public async Task Upload_over_10_mb_returns_413_problem_details()
        {
            var cancellationToken =
                TestContext.Current.CancellationToken;

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    "/api/registry-entries/" +
                    "41e875bc-5083-4789-a950-44b703c22072/" +
                    "documents");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    CreateAdminToken());

            request.Headers.Add(
                "X-Trace-Id",
                TraceId);

            request.Content =
                new DeclaredLengthContent(
                    FileStorageService.MaxUploadRequestSize + 1);

            using var response =
                await client.SendAsync(
                    request,
                    cancellationToken);

            var problem =
                await ReadProblemAsync(response);

            Assert.Equal(
                (HttpStatusCode)413,
                response.StatusCode);

            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            Assert.Equal(
                413,
                problem.Status);

            Assert.Equal(
                TraceId,
                problem.TraceId);

            Assert.Equal(
                TraceId,
                response.Headers
                    .GetValues("X-Trace-Id")
                    .Single());

            Assert.Contains(
                "10 MB",
                problem.Detail);
        }

        [Fact]
        public async Task Duplicate_registry_code_returns_409_problem_details()
        {
            var cancellationToken =
                TestContext.Current.CancellationToken;

            await SeedRegistryTypeAsync(
                cancellationToken);

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    "/api/registry-types");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    CreateAdminToken());

            request.Headers.Add(
                "X-Trace-Id",
                TraceId);

            request.Content =
                JsonContent.Create(new
                {
                    code = "DUPLICAT",
                    name = "Registru duplicat",
                    direction = "In",
                    startNumber = 1,
                    defaultDeadlineDays = 30
                });

            using var response =
                await client.SendAsync(
                    request,
                    cancellationToken);

            var problem =
                await ReadProblemAsync(response);

            Assert.Equal(
                (HttpStatusCode)409,
                response.StatusCode);

            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            Assert.Equal(
                409,
                problem.Status);

            Assert.Equal(
                TraceId,
                problem.TraceId);

            Assert.Contains(
                "Există deja",
                problem.Detail);
        }

        [Fact]
        public async Task Pdf_smoke_returns_pdf()
        {
            var cancellationToken =
                TestContext.Current.CancellationToken;

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/diagnostics/pdf");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    CreateAdminToken());

            request.Headers.Add("X-Trace-Id", TraceId);

            using var response = await client.SendAsync(
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                Assert.Fail(
                    $"Exportul a returnat {(int)response.StatusCode}: {body}");
            }

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.Equal(
                "application/pdf",
                response.Content.Headers.ContentType?.MediaType);

            Assert.Contains(
                "pdf-smoke.pdf",
                response.Content.Headers.ContentDisposition?.ToString()
                    ?? string.Empty);

            Assert.Equal(
                TraceId,
                response.Headers.GetValues("X-Trace-Id").Single());

            var bytes = await response.Content.ReadAsByteArrayAsync(
                cancellationToken);

            Assert.True(bytes.Length > 1000);

            Assert.Equal(
                "%PDF-",
                Encoding.ASCII.GetString(bytes, 0, 5));
        }

        [Fact]
        public async Task Report_export_returns_pdf()
        {
            var cancellationToken =
                TestContext.Current.CancellationToken;

            await SeedPdfReportAsync(cancellationToken);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/report-definitions/pdf-contract-test/export");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    CreateAdminToken());

            request.Headers.Add("X-Trace-Id", TraceId);

            using var response = await client.SendAsync(
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                Assert.Fail(
                    $"Exportul raportului a returnat " +
                    $"{(int)response.StatusCode}: {body}");
            }

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(
                "application/pdf",
                response.Content.Headers.ContentType?.MediaType);
            Assert.Contains(
                "pdf-contract-test.pdf",
                response.Content.Headers.ContentDisposition?.ToString()
                    ?? string.Empty);
            Assert.Equal(
                TraceId,
                response.Headers.GetValues("X-Trace-Id").Single());

            var bytes = await response.Content.ReadAsByteArrayAsync(
                cancellationToken);

            Assert.True(bytes.Length > 1_000);
            Assert.Equal(
                "%PDF-",
                Encoding.ASCII.GetString(bytes, 0, 5));
        }

        [Fact]
        public async Task Draft_report_preview_returns_data_without_persisting_a_definition()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/report-definitions/preview");

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                CreateAdminToken());

            request.Content = JsonContent.Create(new
            {
                datasetKey = "tasks",
                definition = new
                {
                    renderMode = "table",
                    datasetKey = "tasks",
                    parameters = Array.Empty<object>(),
                    columns = new[]
                    {
                        new
                        {
                            field = "title",
                            label = "Titlu sarcină",
                            type = "text",
                            align = "left",
                            widthPct = 100
                        }
                    },
                    sort = new[]
                    {
                        new { field = "title", dir = "asc" }
                    },
                    totals = Array.Empty<object>(),
                    layout = new
                    {
                        orientation = "portrait",
                        title = "Previzualizare nesalvată",
                        showPageNumbers = false
                    }
                }
            });

            using var response = await client.SendAsync(
                request,
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var verificationScope =
                factory.Services.CreateScope();

            var db = verificationScope.ServiceProvider
                .GetRequiredService<DmsDbContext>();

            Assert.False(await db.ReportDefinitions.AnyAsync(
                report => report.code == "preview",
                cancellationToken));
        }



        private static string CreateAdminToken()
        {
            const string signingKey =
                "test-signing-key-with-at-least-32-bytes";

            var securityKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(signingKey));

            var credentials =
                new SigningCredentials(
                    securityKey,
                    SecurityAlgorithms.HmacSha256);

            var token =
                new JwtSecurityToken(
                    issuer: "TestIssuer",
                    audience: "TestAudience",
                    claims:
                    [
                        new Claim(
                    JwtRegisteredClaimNames.Sub,
                    Guid.NewGuid().ToString()),

                new Claim(
                    ClaimTypes.Role,
                    "Admin")
                    ],
                    expires: DateTime.UtcNow.AddMinutes(5),
                    signingCredentials: credentials);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }

        private async Task SeedRegistryTypeAsync(
            CancellationToken cancellationToken)
        {
            await using var scope =
                factory.Services.CreateAsyncScope();

            var db =
                scope.ServiceProvider
                    .GetRequiredService<DmsDbContext>();

            db.RegistryTypes.Add(new RegistryType
            {
                id = Guid.NewGuid(),
                code = "DUPLICAT",
                name = "Registru existent",
                direction = RegistryDirection.In,
                start_number = 1,
                default_deadline_days = 30,
                is_closed = false
            });

            await db.SaveChangesAsync(
                cancellationToken);
        }

        private async Task SeedPdfReportAsync(
            CancellationToken cancellationToken)
        {
            await using var scope =
                factory.Services.CreateAsyncScope();

            var db = scope.ServiceProvider
                .GetRequiredService<DmsDbContext>();

            db.ReportDefinitions.Add(new ReportDefinition
            {
                id = Guid.NewGuid(),
                code = "pdf-contract-test",
                name = "Raport PDF de verificare",
                dataset_key = "tasks",
                definition = JsonDocument.Parse("""
                {
                  "renderMode": "table",
                  "datasetKey": "tasks",
                  "parameters": [],
                  "columns": [
                    {
                      "field": "title",
                      "label": "Titlu sarcină",
                      "type": "text",
                      "align": "left",
                      "widthPct": 100
                    }
                  ],
                  "sort": [
                    { "field": "title", "dir": "asc" }
                  ],
                  "totals": [
                    { "field": "title", "agg": "count" }
                  ],
                  "layout": {
                    "orientation": "portrait",
                    "title": "Raport ș și ț",
                    "subtitle": "Verificare export PDF",
                    "showPageNumbers": true
                  }
                }
                """),
                version = 1,
                is_system = false,
                updated_by_user_id = Guid.NewGuid()
            });

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
