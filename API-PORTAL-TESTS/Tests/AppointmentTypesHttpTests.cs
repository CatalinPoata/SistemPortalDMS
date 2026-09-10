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
using Xunit.Sdk;
using Task = System.Threading.Tasks.Task;

namespace API_PORTAL_TESTS.Tests
{
    public sealed class AppointmentTypesHttpTests
        : IClassFixture<PortalApiFactory>
    {
        private readonly PortalApiFactory factory;

        public AppointmentTypesHttpTests(PortalApiFactory factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task Admin_can_create_and_update_appointment_type()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var client = await CreateClientAsync(
                Role.Admin,
                cancellationToken);
            var code = $"aud-{Guid.NewGuid():N}";

            using var createResponse = await client.PostAsJsonAsync(
                "/api/appointment-types",
                CreateRequest(code),
                cancellationToken);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            using var updateResponse = await client.PutAsJsonAsync(
                $"/api/appointment-types/{code}",
                new
                {
                    name = "Audiență actualizată",
                    description = "Programare la ghișeu.",
                    location = "Camera 2",
                    durationMinutes = 20,
                    requiresConfirmation = false,
                    maxDaysAhead = 14,
                    isActive = true
                },
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            var updated = await updateResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);

            Assert.Equal("Audiență actualizată", updated
                .GetProperty("name").GetString());
            Assert.False(updated.GetProperty("requiresConfirmation")
                .GetBoolean());
            Assert.Equal(20, updated.GetProperty("durationMinutes")
                .GetInt32());
        }

        [Fact]
        public async Task Citizen_cannot_manage_appointment_types()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var client = await CreateClientAsync(
                Role.Citizen,
                cancellationToken);

            using var response = await client.PostAsJsonAsync(
                "/api/appointment-types",
                CreateRequest($"interzis-{Guid.NewGuid():N}"),
                cancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task Public_list_includes_only_active_appointment_types()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var activeCode = $"public-{Guid.NewGuid():N}";
            var inactiveCode = $"privat-{Guid.NewGuid():N}";

            await SeedTypeAsync(activeCode, isActive: true, cancellationToken);
            await SeedTypeAsync(inactiveCode, isActive: false, cancellationToken);

            using var client = factory.CreateClient();
            using var response = await client.GetAsync(
                "/api/public/appointment-types?page=1&pageSize=100",
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            var codes = body.GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("code").GetString())
                .ToArray();

            Assert.Contains(activeCode, codes);
            Assert.DoesNotContain(inactiveCode, codes);
        }

        [Fact]
        public async Task Admin_generates_slots_and_blocked_slot_is_hidden_publicly()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var admin = await CreateClientAsync(
                Role.Admin,
                cancellationToken);
            var code = $"slot-{Guid.NewGuid():N}";

            using (var typeResponse = await admin.PostAsJsonAsync(
                "/api/appointment-types",
                CreateRequest(code),
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Created, typeResponse.StatusCode);
            }

            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
            var weekday = ((int)date.DayOfWeek + 6) % 7 + 1;
            using var generationResponse = await admin.PostAsJsonAsync(
                $"/api/appointment-types/{code}/slots/generate",
                new
                {
                    startDate = date,
                    endDate = date,
                    dayStartsAt = new TimeOnly(9, 0),
                    dayEndsAt = new TimeOnly(10, 0),
                    weekdays = new[] { weekday },
                    capacity = 1,
                    timeZoneId = "UTC"
                },
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, generationResponse.StatusCode);
            var generated = await generationResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            Assert.Equal(2, generated.GetArrayLength());
            var blockedSlotId = generated[0].GetProperty("id").GetGuid();
            Assert.EndsWith("+00:00", generated[0]
                .GetProperty("startsAt").GetString());

            using (var blockResponse = await admin.PutAsJsonAsync(
                $"/api/appointment-types/{code}/slots/{blockedSlotId}/block",
                new { isBlocked = true },
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, blockResponse.StatusCode);
            }

            using var publicClient = factory.CreateClient();
            using var publicResponse = await publicClient.GetAsync(
                $"/api/public/appointment-types/{code}/slots",
                cancellationToken);

            Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
            var available = await publicResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            var ids = available.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid())
                .ToArray();
            Assert.Single(ids);
            Assert.DoesNotContain(blockedSlotId, ids);

            using var dateFilteredResponse = await publicClient.GetAsync(
                $"/api/public/appointment-types/{code}/slots?date={date:yyyy-MM-dd}",
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, dateFilteredResponse.StatusCode);
            var dateFiltered = await dateFilteredResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            Assert.Single(dateFiltered.GetProperty("items").EnumerateArray());
        }

        [Fact]
        public async Task Admin_can_delete_an_unbooked_slot_then_appointment_type()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var admin = await CreateClientAsync(Role.Admin, cancellationToken);
            var code = $"delete-{Guid.NewGuid():N}";

            using (var typeResponse = await admin.PostAsJsonAsync(
                "/api/appointment-types",
                CreateRequest(code),
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Created, typeResponse.StatusCode);
            }

            var slotId = await SeedFutureSlotAsync(code, cancellationToken);

            using (var slotResponse = await admin.DeleteAsync(
                $"/api/appointment-types/{code}/slots/{slotId}",
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.NoContent, slotResponse.StatusCode);
            }

            using var typeDeleteResponse = await admin.DeleteAsync(
                $"/api/appointment-types/{code}",
                cancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, typeDeleteResponse.StatusCode);
        }

        [Fact]
        public async Task Citizen_can_book_and_cancel_an_appointment()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var admin = await CreateClientAsync(
                Role.Admin,
                cancellationToken);
            var code = $"book-{Guid.NewGuid():N}";

            using (var typeResponse = await admin.PostAsJsonAsync(
                "/api/appointment-types",
                CreateRequest(code),
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Created, typeResponse.StatusCode);
            }

            var slotId = await SeedFutureSlotAsync(code, cancellationToken);
            using var citizen = await CreateClientAsync(
                Role.Citizen,
                cancellationToken);

            using var bookingResponse = await citizen.PostAsJsonAsync(
                $"/api/public/appointment-types/{code}/appointments",
                new { slotId, notes = "Observație test" },
                cancellationToken);

            Assert.Equal(HttpStatusCode.Created, bookingResponse.StatusCode);
            var booking = await bookingResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            Assert.Equal("Requested", booking.GetProperty("status").GetString());

            using var mineResponse = await citizen.GetAsync(
                "/api/appointments/mine",
                cancellationToken);
            var mineBody = await mineResponse.Content.ReadAsStringAsync(
                cancellationToken);
            Assert.True(
                mineResponse.StatusCode == HttpStatusCode.OK,
                $"Mine a răspuns {(int)mineResponse.StatusCode}: {mineBody}");
            using var mineDocument = JsonDocument.Parse(mineBody);
            var mine = mineDocument.RootElement;
            var appointmentId = mine[0].GetProperty("id").GetGuid();

            using var cancelResponse = await citizen.PostAsync(
                $"/api/appointments/{appointmentId}/cancel",
                content: null,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

            var cancelled = await cancelResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var slot = await db.AppointmentSlots.SingleAsync(
                item => item.id == slotId,
                cancellationToken);
            Assert.Equal(0, slot.booked_count);
        }

        [Fact]
        public async Task Admin_can_confirm_complete_and_filter_appointments()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var admin = await CreateClientAsync(
                Role.Admin,
                cancellationToken);
            using var citizen = await CreateClientAsync(
                Role.Citizen,
                cancellationToken);
            var code = $"flow-{Guid.NewGuid():N}";

            using (var typeResponse = await admin.PostAsJsonAsync(
                "/api/appointment-types",
                CreateRequest(code),
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Created, typeResponse.StatusCode);
            }

            var slotId = await SeedFutureSlotAsync(code, cancellationToken);
            using var bookingResponse = await citizen.PostAsJsonAsync(
                $"/api/public/appointment-types/{code}/appointments",
                new { slotId },
                cancellationToken);
            Assert.Equal(HttpStatusCode.Created, bookingResponse.StatusCode);
            var booking = await bookingResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            var appointmentId = booking.GetProperty("id").GetGuid();

            using var confirmResponse = await admin.PostAsync(
                $"/api/appointments/{appointmentId}/confirm",
                content: null,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
            using var completeResponse = await admin.PostAsync(
                $"/api/appointments/{appointmentId}/complete",
                content: null,
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

            using var listResponse = await admin.GetAsync(
                $"/api/appointments?typeCode={code}&status=Completed&page=1&pageSize=10",
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            Assert.Equal(1, list.GetProperty("total").GetInt32());
            Assert.Equal(appointmentId, list.GetProperty("items")[0]
                .GetProperty("id").GetGuid());
        }

        [Fact]
        public async Task Reject_requires_reason_and_releases_capacity()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var admin = await CreateClientAsync(
                Role.Admin,
                cancellationToken);
            using var citizen = await CreateClientAsync(
                Role.Citizen,
                cancellationToken);
            var code = $"reject-{Guid.NewGuid():N}";

            using (var typeResponse = await admin.PostAsJsonAsync(
                "/api/appointment-types",
                CreateRequest(code),
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Created, typeResponse.StatusCode);
            }

            var slotId = await SeedFutureSlotAsync(code, cancellationToken);
            using var bookingResponse = await citizen.PostAsJsonAsync(
                $"/api/public/appointment-types/{code}/appointments",
                new { slotId },
                cancellationToken);
            var booking = await bookingResponse.Content
                .ReadFromJsonAsync<JsonElement>(
                    cancellationToken: cancellationToken);
            var appointmentId = booking.GetProperty("id").GetGuid();

            using var missingReasonResponse = await admin.PostAsJsonAsync(
                $"/api/appointments/{appointmentId}/reject",
                new { decisionNote = " " },
                cancellationToken);
            Assert.Equal(
                HttpStatusCode.UnprocessableEntity,
                missingReasonResponse.StatusCode);

            using var rejectResponse = await admin.PostAsJsonAsync(
                $"/api/appointments/{appointmentId}/reject",
                new { decisionNote = "Interval indisponibil." },
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var slot = await db.AppointmentSlots.SingleAsync(
                item => item.id == slotId,
                cancellationToken);
            Assert.Equal(0, slot.booked_count);
            var appointment = await db.Appointments.SingleAsync(
                item => item.id == appointmentId,
                cancellationToken);
            Assert.Equal(AppointmentStatus.Rejected, appointment.status);
            Assert.Equal("Interval indisponibil.", appointment.decision_note);
        }

        [Fact]
        [Trait("Category", "Postgres")]
        public async Task T10_parallel_bookings_never_exceed_capacity()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var connection = Environment.GetEnvironmentVariable(
                "PORTAL_TEST_CONNECTION");
            if (string.IsNullOrWhiteSpace(connection))
            {
                throw SkipException.ForSkip(
                    "PORTAL_TEST_CONNECTION nu este configurată.");
            }

            using var postgresFactory = new PortalApiFactory(connection);
            var admin = await CreateClientForFactoryAsync(
                postgresFactory,
                Role.Admin,
                cancellationToken);
            var code = $"t10-{Guid.NewGuid():N}";

            using (var typeResponse = await admin.PostAsJsonAsync(
                "/api/appointment-types",
                CreateRequest(code),
                cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Created, typeResponse.StatusCode);
            }

            var slotId = await SeedSlotForFactoryAsync(
                postgresFactory,
                code,
                capacity: 3,
                cancellationToken);

            var statuses = await Task.WhenAll(
                Enumerable.Range(0, 10).Select(async _ =>
                {
                    using var citizen = await CreateClientForFactoryAsync(
                        postgresFactory,
                        Role.Citizen,
                        cancellationToken);
                    using var response = await citizen.PostAsJsonAsync(
                        $"/api/public/appointment-types/{code}/appointments",
                        new { slotId },
                        cancellationToken);
                    return response.StatusCode;
                }));

            Assert.Equal(3, statuses.Count(status =>
                status == HttpStatusCode.Created));
            Assert.Equal(7, statuses.Count(status =>
                status == HttpStatusCode.Conflict));

            await using var scope = postgresFactory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var slot = await db.AppointmentSlots.SingleAsync(
                item => item.id == slotId,
                cancellationToken);
            Assert.Equal(3, slot.booked_count);
            Assert.Equal(3, await db.Appointments.CountAsync(
                item => item.slot_id == slotId,
                cancellationToken));
        }

        private async Task<Guid> SeedFutureSlotAsync(
            string code,
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var type = await db.AppointmentTypes.SingleAsync(
                item => item.code == code,
                cancellationToken);
            var slot = new AppointmentSlot
            {
                id = Guid.NewGuid(),
                appointment_type_id = type.id,
                starts_at = DateTimeOffset.UtcNow.AddDays(1),
                ends_at = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(30),
                capacity = 1,
                booked_count = 0,
                is_blocked = false
            };
            db.AppointmentSlots.Add(slot);
            await db.SaveChangesAsync(cancellationToken);
            return slot.id;
        }

        private static async Task<HttpClient> CreateClientForFactoryAsync(
            PortalApiFactory target,
            Role role,
            CancellationToken cancellationToken)
        {
            await using var scope = target.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var tokenService = scope.ServiceProvider
                .GetRequiredService<IJwtTokenService>();
            var user = new User
            {
                id = Guid.NewGuid(),
                email = $"t10-{Guid.NewGuid():N}@example.com",
                full_name = "T10 Test",
                role = role,
                email_confirmed = true,
                is_active = true,
                password_hash = "unused"
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);

            var client = target.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    tokenService.Create(user).AccessToken);
            return client;
        }

        private static async Task<Guid> SeedSlotForFactoryAsync(
            PortalApiFactory target,
            string code,
            int capacity,
            CancellationToken cancellationToken)
        {
            await using var scope = target.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var type = await db.AppointmentTypes.SingleAsync(
                item => item.code == code,
                cancellationToken);
            var slot = new AppointmentSlot
            {
                id = Guid.NewGuid(),
                appointment_type_id = type.id,
                starts_at = DateTimeOffset.UtcNow.AddDays(1),
                ends_at = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(30),
                capacity = capacity,
                booked_count = 0
            };
            db.AppointmentSlots.Add(slot);
            await db.SaveChangesAsync(cancellationToken);
            return slot.id;
        }

        private async Task SeedTypeAsync(
            string code,
            bool isActive,
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            db.AppointmentTypes.Add(new AppointmentType
            {
                id = Guid.NewGuid(),
                code = code,
                name = code,
                duration_minutes = 30,
                max_days_ahead = 30,
                is_active = isActive
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        private async Task<HttpClient> CreateClientAsync(
            Role role,
            CancellationToken cancellationToken)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var tokenService = scope.ServiceProvider
                .GetRequiredService<IJwtTokenService>();
            var user = new User
            {
                id = Guid.NewGuid(),
                email = $"appointment-{Guid.NewGuid():N}@example.com",
                full_name = "Appointment Test",
                role = role,
                email_confirmed = true,
                is_active = true,
                password_hash = "unused"
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);

            var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    tokenService.Create(user).AccessToken);
            return client;
        }

        private static object CreateRequest(string code)
        {
            return new
            {
                code,
                name = "Audiență",
                description = "Întâlnire cu instituția.",
                location = "Camera 1",
                durationMinutes = 30,
                requiresConfirmation = true,
                maxDaysAhead = 30,
                isActive = true
            };
        }
    }
}
