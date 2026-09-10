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
using System.Text.Json;
using Xunit;

namespace API_PORTAL_TESTS.Tests;

public sealed class SurveyHttpTests : IClassFixture<PortalApiFactory>
{
    private readonly PortalApiFactory factory;

    public SurveyHttpTests(PortalApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Published_survey_is_public_and_accepts_anonymous_response()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await CreateClientAsync(Role.Admin, cancellationToken);
        var code = $"feedback-{Guid.NewGuid():N}";

        using var create = await admin.PostAsJsonAsync("/api/surveys", new
        {
            code,
            title = "Feedback",
            description = "<p>Spune-ne părerea.</p><script>alert(1)</script>",
            allowAnonymous = true,
            showResults = true
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var question = await admin.PostAsJsonAsync($"/api/surveys/{code}/questions", new
        {
            key = "satisfactie",
            text = "Cât de mulțumit ești?",
            type = "Rating",
            options = new { min = 1, max = 5 },
            isRequired = true,
            displayOrder = 0
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, question.StatusCode);

        using var publish = await admin.PostAsync($"/api/surveys/{code}/publish", null, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        using var publicClient = factory.CreateClient();
        using var list = await publicClient.GetAsync("/api/public/surveys", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Contains(code, listBody.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("code").GetString()));

        using var details = await publicClient.GetAsync(
            $"/api/public/surveys/{code}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var detailsBody = await details.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: cancellationToken);
        Assert.Equal(code, detailsBody.GetProperty("code").GetString());
        Assert.Single(detailsBody.GetProperty("questions").EnumerateArray());

        using var submit = await publicClient.PostAsJsonAsync($"/api/public/surveys/{code}/responses", new
        {
            answers = new { satisfactie = 5 }
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.True(body.GetProperty("results").GetProperty("totalResponses").GetInt32() >= 1);
    }

    [Fact]
    public async Task Authenticated_user_can_answer_only_once_and_invalid_answers_return_422()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await CreateClientAsync(Role.Admin, cancellationToken);
        var code = $"single-{Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/surveys", new
        {
            code,
            title = "Preferințe",
            allowAnonymous = false
        }, cancellationToken)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/surveys/{code}/questions", new
        {
            key = "canal",
            text = "Canal preferat",
            type = "SingleChoice",
            options = new[] { new { value = "online", label = "Online" }, new { value = "ghiseu", label = "Ghișeu" } },
            isRequired = true,
            displayOrder = 0
        }, cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/surveys/{code}/publish", null, cancellationToken)).StatusCode);

        using var citizen = await CreateClientAsync(Role.Citizen, cancellationToken);
        using var invalid = await citizen.PostAsJsonAsync($"/api/public/surveys/{code}/responses", new
        {
            answers = new { canal = "telefon", necunoscuta = "x" }
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);

        using var first = await citizen.PostAsJsonAsync($"/api/public/surveys/{code}/responses", new
        {
            answers = new { canal = "online" }
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await citizen.PostAsJsonAsync($"/api/public/surveys/{code}/responses", new
        {
            answers = new { canal = "ghiseu" }
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
    }

    private async Task<HttpClient> CreateClientAsync(Role role, CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = new User
        {
            id = Guid.NewGuid(),
            email = $"survey-{Guid.NewGuid():N}@example.com",
            full_name = "Survey Test",
            role = role,
            email_confirmed = true,
            is_active = true,
            password_hash = "unused"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.Create(user).AccessToken);
        return client;
    }
}
