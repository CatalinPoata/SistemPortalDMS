using API_PORTAL.Auth;
using API_PORTAL.Data;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL.Integration;
using API_PORTAL_TESTS.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace API_PORTAL_TESTS.Tests
{
    public sealed class PortalOutboxRetryTests : IClassFixture<PortalApiFactory>
    {
        private readonly PortalApiFactory factory;

        public PortalOutboxRetryTests(PortalApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task T5_Temporary_dms_failure_is_retried_and_registers_once()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            factory.DmsRegistrations.Reset();

            var serviceCode = $"retry-{Guid.NewGuid():N}";
            await SeedPublishedServiceAsync(serviceCode, cancellationToken);
            using var client = await CreateCitizenClientAsync(cancellationToken);

            using var createResponse = await client.PostAsJsonAsync(
                "/api/submissions",
                new
                {
                    serviceCode,
                    values = new { nume = "Ana Popescu" }
                },
                cancellationToken);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            var submissionId = created.GetProperty("id").GetGuid();
            var entryId = Guid.NewGuid();

            factory.DmsRegistrations.Enqueue(new DmsRegistrationDeliveryResult(
                Delivered: false,
                Retryable: true,
                ResponseBody: null,
                Error: "DMS indisponibil temporar."));

            await DispatchOnceAsync(cancellationToken);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
                var pending = await db.OutboxMessages.SingleAsync(
                    item => item.aggregate_id == submissionId,
                    cancellationToken);
                var submission = await db.Submissions.SingleAsync(
                    item => item.id == submissionId,
                    cancellationToken);

                Assert.Equal(OutboxMessageStatus.Pending, pending.status);
                Assert.Equal(1, pending.attempts);
                Assert.Equal("DMS indisponibil temporar.", pending.last_error);
                Assert.True(pending.next_attempt_at > DateTimeOffset.UtcNow);
                Assert.Equal(SubmissionStatus.Submitted, submission.status);

                pending.next_attempt_at = DateTimeOffset.UtcNow.AddSeconds(-1);
                await db.SaveChangesAsync(cancellationToken);
            }

            factory.DmsRegistrations.Enqueue(new DmsRegistrationDeliveryResult(
                Delivered: true,
                Retryable: false,
                ResponseBody: JsonSerializer.Serialize(new
                {
                    entryId,
                    registryTypeCode = "INTRARI",
                    registryName = "Registru de intrări",
                    number = 1L,
                    year = 2026,
                    displayNumber = "1/2026",
                    registeredAt = DateTimeOffset.Parse(
                        "2026-09-07T12:00:00+00:00"),
                    status = "Registered"
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                Error: null));

            await DispatchOnceAsync(cancellationToken);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
                var delivered = await db.OutboxMessages.SingleAsync(
                    item => item.aggregate_id == submissionId,
                    cancellationToken);
                var submission = await db.Submissions.SingleAsync(
                    item => item.id == submissionId,
                    cancellationToken);

                Assert.Equal(2, factory.DmsRegistrations.DeliveryCount);
                Assert.Equal(OutboxMessageStatus.Delivered, delivered.status);
                Assert.Equal(1, delivered.attempts);
                Assert.Null(delivered.last_error);
                Assert.NotNull(delivered.delivered_at);
                Assert.Equal(SubmissionStatus.Registered, submission.status);
                Assert.Equal(entryId, submission.dms_entry_id);
                Assert.Equal(1, submission.registry_number);
                Assert.Equal(2026, submission.registry_year);
                Assert.Equal("1/2026", submission.registry_display_number);
                Assert.Equal(1, await db.SubmissionEvents.CountAsync(item =>
                    item.submission_id == submissionId &&
                    item.type == SubmissionEventType.Registered,
                    cancellationToken));
                Assert.Equal(1, await db.Notifications.CountAsync(item =>
                    item.user_id == submission.user_id &&
                    item.link_url == $"/cereri/{submissionId}",
                    cancellationToken));
            }
        }

        private async Task DispatchOnceAsync(CancellationToken cancellationToken)
        {
            var dispatcher = new PortalOutboxDispatcher(
                factory.Services.GetRequiredService<IServiceScopeFactory>(),
                factory.Services.GetRequiredService<ILogger<PortalOutboxDispatcher>>());

            await dispatcher.DispatchOnceAsync(cancellationToken);
        }

        private async Task SeedPublishedServiceAsync(
            string code,
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            db.ServiceDefinitions.Add(new ServiceDefinition
            {
                id = Guid.NewGuid(),
                code = code,
                title = "Serviciu pentru retry outbox",
                registry_type_code = "INTRARI",
                form_schema = JsonDocument.Parse("""
                    {
                      "sections": [
                        {
                          "key": "solicitant",
                          "title": "Date solicitant",
                          "fields": [
                            {
                              "key": "nume",
                              "label": "Nume complet",
                              "type": "text",
                              "required": true,
                              "maxLength": 200
                            }
                          ]
                        }
                      ]
                    }
                    """),
                schema_version = 1,
                is_published = true,
                max_attachments = 0
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        private async Task<HttpClient> CreateCitizenClientAsync(
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var citizen = new User
            {
                id = Guid.NewGuid(),
                email = $"retry-{Guid.NewGuid():N}@example.com",
                full_name = "Cetățean test",
                role = Role.Citizen,
                password_hash = "unused",
                email_confirmed = true,
                is_active = true
            };

            db.Users.Add(citizen);
            await db.SaveChangesAsync(cancellationToken);

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false
            });
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    tokens.Create(citizen).AccessToken);
            return client;
        }
    }
}
