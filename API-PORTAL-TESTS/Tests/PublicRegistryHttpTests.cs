using API_PORTAL.Auth;
using API_PORTAL.Data;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL_TESTS.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace API_PORTAL_TESTS.Tests;

public sealed class PublicRegistryHttpTests : IClassFixture<PortalApiFactory>
{
    private readonly PortalApiFactory factory;

    public PublicRegistryHttpTests(PortalApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Published_registry_entry_and_document_are_publicly_downloadable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await CreateClientAsync(Role.Admin, cancellationToken);
        var code = $"acte-{Guid.NewGuid():N}";

        using var registry = await admin.PostAsJsonAsync("/api/public-registries", new
        {
            code,
            name = "Registrul actelor",
            description = "Acte publicate"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, registry.StatusCode);

        using var entry = await admin.PostAsJsonAsync($"/api/public-registries/{code}/entries", new
        {
            positionNumber = "HCL 1",
            title = "Hotărâre privind bugetul",
            entryDate = "2026-09-07",
            description = "Descrierea actului"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, entry.StatusCode);
        var entryBody = await entry.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var entryId = entryBody.GetProperty("id").GetGuid();

        using var multipart = new MultipartFormDataContent();
        var pdf = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.7\npublic registry test"));
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        multipart.Add(pdf, "file", "hotarare.pdf");
        multipart.Add(new StringContent("0"), "displayOrder");
        using var upload = await admin.PostAsync($"/api/public-registries/{code}/entries/{entryId}/documents", multipart, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/public-registries/{code}/entries/{entryId}/publish", null, cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/public-registries/{code}/publish", null, cancellationToken)).StatusCode);

        using var publicClient = factory.CreateClient();
        using var publicResponse = await publicClient.GetAsync($"/api/public/registries/{code}?search=buget&page=1&pageSize=25", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        var publicBody = await publicResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var publicEntry = Assert.Single(publicBody.GetProperty("entries").EnumerateArray());
        var downloadUrl = publicEntry.GetProperty("documents")[0].GetProperty("downloadUrl").GetString();
        Assert.False(string.IsNullOrWhiteSpace(downloadUrl));

        using var download = await publicClient.GetAsync(downloadUrl, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        var bytes = await download.Content.ReadAsByteArrayAsync(cancellationToken);
        Assert.Equal((byte)'%', bytes[0]);
    }

    [Fact]
    public async Task Unpublished_registry_is_not_visible_publicly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = await CreateClientAsync(Role.Admin, cancellationToken);
        var code = $"privat-{Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/public-registries", new
        {
            code,
            name = "Registru privat"
        }, cancellationToken)).StatusCode);

        using var publicClient = factory.CreateClient();
        using var response = await publicClient.GetAsync($"/api/public/registries/{code}", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private async Task<HttpClient> CreateClientAsync(Role role, CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = new User
        {
            id = Guid.NewGuid(),
            email = $"registry-{Guid.NewGuid():N}@example.com",
            full_name = "Registry Test",
            role = role,
            email_confirmed = true,
            is_active = true,
            password_hash = "unused"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

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
