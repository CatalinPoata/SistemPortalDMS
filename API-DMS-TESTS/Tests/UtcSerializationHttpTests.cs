using API_DMS.Auth;
using API_DMS.Data;
using API_DMS.Seed;
using API_DMS_TESTS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace API_DMS_TESTS.Tests;

public sealed class UtcSerializationHttpTests : IClassFixture<DmsApiFactory>
{
    private readonly DmsApiFactory factory;

    public UtcSerializationHttpTests(DmsApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task T14_Romanian_culture_does_not_change_utc_api_timestamps()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ro-RO");
            CultureInfo.CurrentUICulture = new CultureInfo("ro-RO");

            var cancellationToken = TestContext.Current.CancellationToken;
            var accessToken = await SeedAndCreateAdminTokenAsync(
                cancellationToken);
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/registry-entries?page=1&pageSize=25");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

            using var response = await client.SendAsync(
                request,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var document = JsonDocument.Parse(await response.Content
                .ReadAsStreamAsync(cancellationToken));
            var registeredAt = document.RootElement
                .GetProperty("items")[0]
                .GetProperty("registeredAt")
                .GetString();

            Assert.NotNull(registeredAt);
            Assert.EndsWith("+00:00", registeredAt);
            Assert.Equal(TimeSpan.Zero, DateTimeOffset.Parse(
                registeredAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind).Offset);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private async Task<string> SeedAndCreateAdminTokenAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await DmsDbSeeder.SeedAsync(scope.ServiceProvider, cancellationToken);

        var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
        var tokenService = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>();
        var admin = await db.Users.SingleAsync(
            item => item.email == "admin@example.com",
            cancellationToken);

        return tokenService.Create(admin).AccessToken;
    }
}
