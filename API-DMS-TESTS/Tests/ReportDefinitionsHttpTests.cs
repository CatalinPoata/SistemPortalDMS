using API_DMS.Data;
using API_DMS.Reports;
using API_DMS.Seed;
using API_DMS_TESTS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shared.Reporting;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace API_DMS_TESTS.Tests;

public sealed class ReportDefinitionsHttpTests
    : IClassFixture<DmsApiFactory>
{
    private const string TraceId =
        "22222222222222222222222222222222";

    private readonly DmsApiFactory factory;
    private readonly HttpClient client;

    public ReportDefinitionsHttpTests(DmsApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Registry_preview_returns_paged_rows_and_totals()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync(cancellationToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
        var registryId = await db.RegistryTypes
            .Where(item => item.code == "REG-GEN")
            .Select(item => item.id)
            .SingleAsync(cancellationToken);

        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/report-definitions/registru-intrari-iesiri/preview" +
            $"?registryTypeId={registryId}" +
            "&dateFrom=2020-01-01&dateTo=2100-12-31" +
            "&page=1&pageSize=1");

        using var response = await client.SendAsync(
            request,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            TraceId,
            response.Headers.GetValues("X-Trace-Id").Single());

        var preview = await response.Content
            .ReadFromJsonAsync<PreviewResponse>(cancellationToken);

        Assert.NotNull(preview);
        Assert.Equal(1, preview!.Meta.Page);
        Assert.Equal(1, preview.Meta.PageSize);
        Assert.True(preview.Meta.Total >= 2);
        Assert.Single(preview.Rows);
        Assert.Equal(preview.Meta.Total, preview.Totals["count:number"]);
    }

    [Fact]
    public async Task Registry_csv_export_uses_selected_columns_and_utf8_bom()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync(cancellationToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
        var registryId = await db.RegistryTypes
            .Where(item => item.code == "REG-GEN")
            .Select(item => item.id)
            .SingleAsync(cancellationToken);

        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/report-definitions/registru-intrari-iesiri/export.csv" +
            $"?registryTypeId={registryId}" +
            "&dateFrom=2020-01-01&dateTo=2100-12-31");
        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var csv = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.StartsWith("\uFEFFNr.,Data,Solicitant", csv);
        Assert.Contains("Obiectul lucrării", csv);
    }

    [Fact]
    public async Task Registry_preview_rejects_reversed_date_range_with_422()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync(cancellationToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
        var registryId = await db.RegistryTypes
            .Where(item => item.code == "REG-GEN")
            .Select(item => item.id)
            .SingleAsync(cancellationToken);

        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/report-definitions/registru-intrari-iesiri/preview" +
            $"?registryTypeId={registryId}" +
            "&dateFrom=2026-09-30&dateTo=2026-09-01");

        using var response = await client.SendAsync(
            request,
            cancellationToken);

        var problem = await ReadProblemAsync(response, cancellationToken);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal(422, problem.Status);
        Assert.Equal(TraceId, problem.TraceId);
        Assert.NotNull(problem.Errors);
        Assert.Contains("dateTo", problem.Errors!.Keys);
    }

    [Fact]
    public async Task Registry_preview_rejects_page_size_above_server_limit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync(cancellationToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
        var registryId = await db.RegistryTypes
            .Where(item => item.code == "REG-GEN")
            .Select(item => item.id)
            .SingleAsync(cancellationToken);

        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/report-definitions/registru-intrari-iesiri/preview" +
            $"?registryTypeId={registryId}" +
            "&dateFrom=2020-01-01&dateTo=2100-12-31&pageSize=101");

        using var response = await client.SendAsync(
            request,
            cancellationToken);

        var problem = await ReadProblemAsync(response, cancellationToken);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal(422, problem.Status);
        Assert.NotNull(problem.Errors);
        Assert.Contains("pageSize", problem.Errors!.Keys);
    }

    [Fact]
    public async Task Missing_report_returns_404_problem_details()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/report-definitions/raport-inexistent/preview");

        using var response = await client.SendAsync(
            request,
            cancellationToken);

        var problem = await ReadProblemAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(404, problem.Status);
        Assert.Equal(
            "/api/report-definitions/raport-inexistent/preview",
            problem.Instance);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public void Report_html_escapes_values_before_pdf_rendering()
    {
        using var definition = JsonDocument.Parse("""
        {
          "renderMode": "table",
          "datasetKey": "registry_entries",
          "parameters": [],
          "columns": [
            {
              "field": "subject",
              "label": "Subiect",
              "type": "text",
              "align": "left",
              "widthPct": 100
            }
          ],
          "sort": [],
          "totals": [],
          "layout": {
            "orientation": "portrait",
            "title": "Raport sigur",
            "showPageNumbers": false
          }
        }
        """);

        var report = new API_DMS.Entities.ReportDefinition
        {
            id = Guid.NewGuid(),
            code = "html-safety-test",
            name = "Raport sigur",
            dataset_key = "registry_entries",
            definition = definition
        };

        var preview = new API_DMS.DTO.Reports.ReportPreviewResponse(
            [
                new(
                    "subject",
                    "Subiect",
                    "text",
                    "left",
                    100,
                    null)
            ],
            [
                new Dictionary<string, object?>
                {
                    ["subject"] = "<script>alert('x')</script> & Ștefan"
                }
            ],
            new Dictionary<string, decimal>(),
            new(1, 25, 1));

        var rendered = DmsReportHtmlDocument.Create(
            report,
            preview,
            new Microsoft.AspNetCore.Http.QueryCollection());

        Assert.DoesNotContain("<script>alert", rendered.Html);
        Assert.Contains(
            "&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt; &amp; Ștefan",
            rendered.Html);
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateAdminToken());
        request.Headers.Add("X-Trace-Id", TraceId);
        return request;
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await DmsDbSeeder.SeedAsync(
            scope.ServiceProvider,
            cancellationToken);
    }

    private static string CreateAdminToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            "test-signing-key-with-at-least-32-bytes"));
        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "TestIssuer",
            audience: "TestAudience",
            claims:
            [
                new Claim(
                    JwtRegisteredClaimNames.Sub,
                    Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<ProblemResponse> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content
            .ReadFromJsonAsync<ProblemResponse>(cancellationToken);

        Assert.NotNull(problem);
        return problem!;
    }

    private sealed class PreviewResponse
    {
        [JsonPropertyName("rows")]
        public List<Dictionary<string, JsonElement>> Rows { get; set; } = [];

        [JsonPropertyName("totals")]
        public Dictionary<string, decimal> Totals { get; set; } = [];

        [JsonPropertyName("meta")]
        public PreviewMeta Meta { get; set; } = new();
    }

    private sealed class PreviewMeta
    {
        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    private sealed class ProblemResponse
    {
        [JsonPropertyName("status")]
        public int? Status { get; set; }

        [JsonPropertyName("instance")]
        public string? Instance { get; set; }

        [JsonPropertyName("traceId")]
        public string? TraceId { get; set; }

        [JsonPropertyName("errors")]
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
