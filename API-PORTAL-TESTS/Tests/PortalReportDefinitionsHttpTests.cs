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

namespace API_PORTAL_TESTS.Tests;

public sealed class PortalReportDefinitionsHttpTests : IClassFixture<PortalApiFactory>
{
    private readonly PortalApiFactory factory;

    public PortalReportDefinitionsHttpTests(PortalApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Admin_can_create_preview_and_version_a_submission_report()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (admin, service, _) = await SeedAsync(cancellationToken);
        using var client = CreateClient(admin);
        var code = $"cereri-test-{Guid.NewGuid():N}";

        using var create = await client.PostAsJsonAsync(
            "/api/report-definitions",
            new
            {
                code,
                name = "Cereri test",
                datasetKey = "submissions",
                definition = SubmissionDefinition()
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(1, created.GetProperty("version").GetInt32());

        using var preview = await client.GetAsync(
            $"/api/report-definitions/{code}/preview?serviceId={service.id}&page=1&pageSize=25",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(1, previewBody.GetProperty("meta").GetProperty("total").GetInt32());
        Assert.Equal("Cerere cu diacritice Știință", previewBody.GetProperty("rows")[0].GetProperty("service_title").GetString());
        Assert.Equal(1m, previewBody.GetProperty("totals").GetProperty("count:status").GetDecimal());
        Assert.Equal(1m, previewBody.GetProperty("totals").GetProperty("sum:schema_version").GetDecimal());
        Assert.Equal(1m, previewBody.GetProperty("totals").GetProperty("avg:schema_version").GetDecimal());

        using var export = await client.PostAsync(
            $"/api/report-definitions/{code}/export?serviceId={service.id}",
            content: null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/pdf", export.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF-", await export.Content.ReadAsStringAsync(cancellationToken));
        Assert.Contains("Cereri", factory.PdfRenderer.LastHtml);

        using var csv = await client.GetAsync(
            $"/api/report-definitions/{code}/export.csv?serviceId={service.id}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        var csvText = await csv.Content.ReadAsStringAsync(cancellationToken);
        Assert.StartsWith("\uFEFFServiciu,Stare", csvText);
        Assert.Contains("Cerere cu diacritice Știință", csvText);

        using var update = await client.PutAsJsonAsync(
            $"/api/report-definitions/{code}",
            new
            {
                name = "Cereri test actualizat",
                datasetKey = "submissions",
                definition = SubmissionDefinition(),
                expectedVersion = 1
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(2, updated.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Portal_report_rejects_unknown_dataset_fields_and_citizen_access()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (admin, _, citizen) = await SeedAsync(cancellationToken);
        using var adminClient = CreateClient(admin);

        using var invalid = await adminClient.PostAsJsonAsync(
            "/api/report-definitions",
            new
            {
                code = $"invalid-{Guid.NewGuid():N}",
                name = "Raport invalid",
                datasetKey = "submissions",
                definition = new
                {
                    renderMode = "table",
                    datasetKey = "submissions",
                    parameters = Array.Empty<object>(),
                    columns = new[]
                    {
                        new { field = "sql", label = "SQL", type = "text", align = "left", widthPct = 100 }
                    },
                    sort = Array.Empty<object>(),
                    totals = Array.Empty<object>(),
                    layout = new { orientation = "portrait", title = "Raport", showPageNumbers = true }
                }
            },
            cancellationToken);
        Assert.Equal((HttpStatusCode)422, invalid.StatusCode);
        var problem = await invalid.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.True(problem.GetProperty("errors").TryGetProperty("definition.columns[0].field", out _));

        using var citizenClient = CreateClient(citizen);
        using var forbidden = await citizenClient.GetAsync("/api/report-definitions/datasets", cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Admin_submission_list_filters_by_applicant_and_denies_citizens()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (admin, service, citizen) = await SeedAsync(cancellationToken);
        using var adminClient = CreateClient(admin);

        using var response = await adminClient.GetAsync(
            $"/api/admin/submissions?serviceCode={service.code}&applicant=Ștefan&page=1&pageSize=25",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.True(body.GetProperty("total").GetInt32() >= 1);
        Assert.Contains(
            body.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("serviceCode").GetString() == service.code &&
                item.GetProperty("applicantName").GetString() == citizen.full_name);

        var submissionId = body.GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("serviceCode").GetString() == service.code)
            .GetProperty("id")
            .GetGuid();

        using var details = await adminClient.GetAsync(
            $"/api/admin/submissions/{submissionId}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var detailBody = await details.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: cancellationToken);
        Assert.Equal(citizen.full_name,
            detailBody.GetProperty("applicantName").GetString());
        Assert.Equal(service.code,
            detailBody.GetProperty("serviceCode").GetString());
        Assert.True(detailBody.TryGetProperty("events", out _));
        Assert.True(detailBody.TryGetProperty("values", out _));

        using var citizenClient = CreateClient(citizen);
        using var forbidden = await citizenClient.GetAsync(
            "/api/admin/submissions",
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var forbiddenDetails = await citizenClient.GetAsync(
            $"/api/admin/submissions/{submissionId}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDetails.StatusCode);
    }

    private async Task<(User Admin, ServiceDefinition Service, User Citizen)> SeedAsync(CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        var admin = NewUser(Role.Admin, "Administrator Rapoarte");
        var citizen = NewUser(Role.Citizen, "Ștefan Țăranu");
        var service = new ServiceDefinition
        {
            id = Guid.NewGuid(),
            code = $"serviciu-raport-{Guid.NewGuid():N}",
            title = "Cerere cu diacritice Știință",
            registry_type_code = "REG-GEN",
            form_schema = JsonDocument.Parse("{\"sections\":[]}"),
            is_published = true
        };
        var submission = new Submission
        {
            id = Guid.NewGuid(),
            external_id = Guid.NewGuid(),
            service_id = service.id,
            service = service,
            user_id = citizen.id,
            user = citizen,
            schema_version = 1,
            form_snapshot = JsonDocument.Parse("{\"sections\":[]}"),
            values = JsonDocument.Parse("{}"),
            submitted_at = new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero),
            status = SubmissionStatus.Submitted
        };
        db.AddRange(admin, citizen, service, submission);
        await db.SaveChangesAsync(cancellationToken);
        return (admin, service, citizen);
    }

    private static object SubmissionDefinition() => new
    {
        renderMode = "table",
        datasetKey = "submissions",
        parameters = new[]
        {
            new { name = "serviceId", type = "lookup", source = "service_definitions", label = "Serviciu", required = true }
        },
        columns = new object[]
        {
            new { field = "service_title", label = "Serviciu", type = "text", align = "left", widthPct = 60 },
            new { field = "status", label = "Stare", type = "code", align = "center", widthPct = 40 }
        },
        sort = new[] { new { field = "service_title", dir = "asc" } },
        totals = new object[]
        {
            new { field = "status", agg = "count" },
            new { field = "schema_version", agg = "sum" },
            new { field = "schema_version", agg = "avg" }
        },
        layout = new { orientation = "portrait", title = "Cereri", subtitle = "Serviciul {serviceId}", showPageNumbers = true }
    };

    private User NewUser(Role role, string fullName) => new()
    {
        id = Guid.NewGuid(),
        email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@example.com",
        full_name = fullName,
        role = role,
        password_hash = "unused",
        email_confirmed = true,
        is_active = true
    };

    private HttpClient CreateClient(User user)
    {
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.Create(user).AccessToken);
        return client;
    }
}
