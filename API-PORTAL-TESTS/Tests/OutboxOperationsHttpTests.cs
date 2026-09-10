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
    public sealed class OutboxOperationsHttpTests : IClassFixture<PortalApiFactory>
    {
        private readonly PortalApiFactory factory;

        public OutboxOperationsHttpTests(PortalApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task Admin_can_list_failed_messages_and_retry_one()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var failedId = Guid.NewGuid();
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
                db.OutboxMessages.AddRange(
                    CreateMessage(failedId, OutboxMessageStatus.Failed),
                    CreateMessage(Guid.NewGuid(), OutboxMessageStatus.Pending));
                await db.SaveChangesAsync(cancellationToken);
            }

            using var client = await CreateAdminClientAsync(cancellationToken);
            using var list = await client.GetAsync(
                "/api/integration/outbox",
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var messages = await list.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            Assert.Single(messages.EnumerateArray());
            Assert.Equal(failedId, messages[0].GetProperty("id").GetGuid());

            using var retry = await client.PostAsync(
                $"/api/integration/outbox/{failedId}/retry",
                null,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            var retried = await retry.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            Assert.Equal("Pending", retried.GetProperty("status").GetString());
            Assert.Equal(0, retried.GetProperty("attempts").GetInt32());

            await using var verificationScope = factory.Services.CreateAsyncScope();
            var verificationDb = verificationScope.ServiceProvider
                .GetRequiredService<PortalDbContext>();
            var persisted = await verificationDb.OutboxMessages.SingleAsync(
                item => item.id == failedId,
                cancellationToken);
            Assert.Equal(OutboxMessageStatus.Pending, persisted.status);
            Assert.Null(persisted.last_error);
        }

        private static OutboxMessage CreateMessage(
            Guid id,
            OutboxMessageStatus status)
        {
            return new OutboxMessage
            {
                id = id,
                aggregate_type = "Submission",
                aggregate_id = Guid.NewGuid(),
                event_type = "Submission.Register",
                payload = JsonDocument.Parse("{}"),
                status = status,
                attempts = status == OutboxMessageStatus.Failed ? 8 : 0,
                next_attempt_at = DateTimeOffset.UtcNow,
                last_error = status == OutboxMessageStatus.Failed ? "DMS indisponibil" : null
            };
        }

        private async Task<HttpClient> CreateAdminClientAsync(
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var user = new User
            {
                id = Guid.NewGuid(),
                email = $"outbox-{Guid.NewGuid():N}@example.com",
                full_name = "Administrator test",
                role = Role.Admin,
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
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                tokens.Create(user).AccessToken);
            return client;
        }
    }
}
