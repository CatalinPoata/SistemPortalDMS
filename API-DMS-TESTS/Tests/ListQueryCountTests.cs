using API_DMS.Auth;
using API_DMS.Data;
using API_DMS.Entities;
using API_DMS.Entities.Base;
using API_DMS_TESTS.Data;
using API_DMS_TESTS.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;
using Xunit.Sdk;
using Task = System.Threading.Tasks.Task;

namespace API_DMS_TESTS.Tests;

public sealed class ListQueryCountTests
{
    [Fact]
    public async Task N7_Registry_entries_list_uses_two_selects_for_any_page_size()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = Environment.GetEnvironmentVariable(
            "DMS_TEST_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw SkipException.ForSkip(
                "Variabila DMS_TEST_CONNECTION nu este configurată.");
        }

        var counter = new RequestSqlCommandCounter();
        using var factory = new DmsApiFactory(connectionString, counter);

        var registryId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedAsync(
            connectionString,
            registryId,
            userId,
            cancellationToken);

        try
        {
            using var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });

            await using var scope = factory.Services.CreateAsyncScope();
            var tokenService = scope.ServiceProvider
                .GetRequiredService<IJwtTokenService>();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    tokenService.Create(new User
                    {
                        id = userId,
                        email = "n7-admin@example.test",
                        full_name = "Administrator N7",
                        role = Role.Admin
                    }).AccessToken);
            client.DefaultRequestHeaders.Add(
                RequestSqlCommandCounter.HeaderName,
                counter.RequestId);

            counter.Reset();

            using var response = await client.GetAsync(
                $"/api/registry-entries?registryTypeId={registryId}&page=1&pageSize=8",
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var payload = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            Assert.Equal(8, payload.RootElement.GetProperty("items").GetArrayLength());
            Assert.Equal(8, payload.RootElement.GetProperty("total").GetInt32());

            Assert.Equal(2, counter.SelectCommandCount);
        }
        finally
        {
            await CleanupAsync(
                connectionString,
                registryId,
                userId,
                cancellationToken);
        }
    }

    private static async Task SeedAsync(
        string connectionString,
        Guid registryId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var db = TestDbContextFactory.CreatePostgres(connectionString);
        var registry = new RegistryType
        {
            id = registryId,
            code = $"N7{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            name = "Registru test N7",
            direction = RegistryDirection.In,
            start_number = 1,
            default_deadline_days = 30
        };
        var user = new User
        {
            id = userId,
            email = "n7-admin@example.test",
            full_name = "Administrator N7",
            password_hash = "unused",
            role = Role.Admin,
            email_confirmed = true,
            is_active = true
        };

        db.AddRange(registry, user);

        var now = DateTimeOffset.UtcNow;
        for (var number = 1; number <= 8; number++)
        {
            db.RegistryEntries.Add(new RegistryEntry
            {
                id = Guid.NewGuid(),
                registry_type_id = registryId,
                year = now.Year,
                number = number,
                direction = EntryDirection.In,
                registered_at = now.AddMinutes(number),
                deadline = DateOnly.FromDateTime(now.UtcDateTime.AddDays(30)),
                subject = $"Poziție N7 {number}",
                applicant_name = $"Solicitant N7 {number}",
                status = EntryStatus.Registered,
                created_by_user_id = userId
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task CleanupAsync(
        string connectionString,
        Guid registryId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var db = TestDbContextFactory.CreatePostgres(connectionString);
        var entries = await db.RegistryEntries
            .Where(item => item.registry_type_id == registryId)
            .ToListAsync(cancellationToken);
        db.RegistryEntries.RemoveRange(entries);

        var registry = await db.RegistryTypes.SingleOrDefaultAsync(
            item => item.id == registryId,
            cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(
            item => item.id == userId,
            cancellationToken);

        if (registry is not null)
        {
            db.RegistryTypes.Remove(registry);
        }

        if (user is not null)
        {
            db.Users.Remove(user);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
