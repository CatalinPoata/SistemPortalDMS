using API_PORTAL.Data;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL_TESTS.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Asn1.Cmp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace API_PORTAL_TESTS.Tests
{
    public sealed class AccountAuthTests
    {
        private const string ResetSeedPassword = "Citizen123!ChangeMe";
        private const string ResetNewPassword = "NewPassword123!ChangeMe";
        private const string ResetOtherPassword = "OtherPassword456!ChangeMe";
        private const string ResetRefreshCookieName = "__Host-portal-refresh";

        [Fact]
        [Trait("Category", "Postgres")]
        public async Task Password_reset_is_single_use_and_revokes_all_sessions()
        {
            var ct = TestContext.Current.CancellationToken;

            using var factory = CreatePasswordResetFactory();
            using var client = CreateRefreshClient(factory);

            var session = await CreateSessionAsync(factory, client, ct);

            try
            {
                var secondSession = await LoginSameUserAsync(
                    factory, client, session, ResetSeedPassword, ct);

                await using (var scope = factory.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider
                        .GetRequiredService<PortalDbContext>();

                    var activeSessions = await db.RefreshTokens.CountAsync(
                        token => token.user_id == session.UserId &&
                                 token.revoked_at == null,
                        ct);

                    Assert.Equal(2, activeSessions);
                }

                var resetToken = await RequestResetTokenAsync(
                    factory, client, session.UserId, ct);

                using var resetResponse = await SendPasswordResetAsync(
                    client, resetToken.RawToken, ResetNewPassword, ct);

                Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

                using var replayResponse = await SendPasswordResetAsync(
                    client, resetToken.RawToken, ResetOtherPassword, ct);

                await AssertResetRejectedAsync(replayResponse, ct);

                await AssertResetStateAsync(
                    factory, session.UserId, resetToken.Id,
                    ResetNewPassword, ct);

                using var firstRefresh = await SendRefreshAsync(
                    client, session, ct);

                using var secondRefresh = await SendRefreshAsync(
                    client, secondSession, ct);

                Assert.Equal(HttpStatusCode.Unauthorized, firstRefresh.StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, secondRefresh.StatusCode);

                await LoginSameUserAsync(
                    factory, client, session, ResetNewPassword, ct);
            }
            finally
            {
                await CleanupPasswordResetUserAsync(factory, session.UserId);
            }
        }

        [Fact]
        [Trait("Category", "Postgres")]
        public async Task Password_reset_expired_token_preserves_password_and_session()
        {
            var ct = TestContext.Current.CancellationToken;

            using var factory = CreatePasswordResetFactory();
            using var client = CreateRefreshClient(factory);

            var session = await CreateSessionAsync(factory, client, ct);

            try
            {
                var resetToken = await RequestResetTokenAsync(
                    factory, client, session.UserId, ct);

                await using var scope = factory.Services.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<PortalDbContext>();

                var originalHash = await db.Users
                    .Where(user => user.id == session.UserId)
                    .Select(user => user.password_hash)
                    .SingleAsync(ct);

                var expiredAt = DateTimeOffset.UtcNow.AddHours(-1);

                await db.AccountTokens
                    .Where(token =>
                        token.id == resetToken.Id &&
                        token.user_id == session.UserId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            token => token.expires_at, expiredAt),
                        ct);

                using var response = await SendPasswordResetAsync(
                    client, resetToken.RawToken, ResetNewPassword, ct);

                await AssertResetRejectedAsync(response, ct);

                var currentHash = await db.Users
                    .Where(user => user.id == session.UserId)
                    .Select(user => user.password_hash)
                    .SingleAsync(ct);

                var storedToken = await db.AccountTokens
                    .AsNoTracking()
                    .SingleAsync(token => token.id == resetToken.Id, ct);

                Assert.Equal(originalHash, currentHash);
                Assert.Null(storedToken.consumed_at);

                using var refreshResponse = await SendRefreshAsync(
                    client, session, ct);

                Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
            }
            finally
            {
                await CleanupPasswordResetUserAsync(factory, session.UserId);
            }
        }

        [Fact]
        [Trait("Category", "Postgres")]
        public async Task Password_reset_parallel_requests_consume_token_once()
        {
            var ct = TestContext.Current.CancellationToken;

            using var factory = CreatePasswordResetFactory();
            using var client = CreateRefreshClient(factory);

            var session = await CreateSessionAsync(factory, client, ct);

            try
            {
                var resetToken = await RequestResetTokenAsync(
                    factory, client, session.UserId, ct);

                var responses = await Task.WhenAll(
                    SendPasswordResetAsync(
                        client, resetToken.RawToken, ResetNewPassword, ct),
                    SendPasswordResetAsync(
                        client, resetToken.RawToken, ResetOtherPassword, ct));

                using var first = responses[0];
                using var second = responses[1];

                Assert.Equal(
                    1,
                    responses.Count(response =>
                        response.StatusCode == HttpStatusCode.OK));

                Assert.Equal(
                    1,
                    responses.Count(response =>
                        response.StatusCode == HttpStatusCode.UnprocessableEntity));

                var winningPassword = first.StatusCode == HttpStatusCode.OK
                    ? ResetNewPassword
                    : ResetOtherPassword;

                var rejectedResponse = first.StatusCode == HttpStatusCode.OK
                    ? second
                    : first;

                await AssertResetRejectedAsync(rejectedResponse, ct);

                await AssertResetStateAsync(
                    factory, session.UserId, resetToken.Id,
                    winningPassword, ct);
            }
            finally
            {
                await CleanupPasswordResetUserAsync(factory, session.UserId);
            }
        }

        [Fact]
        [Trait("Category", "Postgres")]
        public async Task Password_reset_parallel_with_refresh_leaves_no_active_tokens()
        {
            var ct = TestContext.Current.CancellationToken;

            using var factory = CreatePasswordResetFactory();
            using var client = CreateRefreshClient(factory);

            var session = await CreateSessionAsync(factory, client, ct);

            try
            {
                var resetToken = await RequestResetTokenAsync(
                    factory, client, session.UserId, ct);

                var responses = await Task.WhenAll(
                    SendPasswordResetAsync(
                        client, resetToken.RawToken, ResetNewPassword, ct),
                    SendRefreshAsync(client, session, ct));

                using var resetResponse = responses[0];
                using var refreshResponse = responses[1];

                Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

                Assert.True(
                    refreshResponse.StatusCode is
                        HttpStatusCode.OK or HttpStatusCode.Unauthorized,
                    $"Refresh a întors {(int)refreshResponse.StatusCode}.");

                await AssertResetStateAsync(
                    factory, session.UserId, resetToken.Id,
                    ResetNewPassword, ct);

                using var originalTokenResponse = await SendRefreshAsync(
                    client, session, ct);

                Assert.Equal(
                    HttpStatusCode.Unauthorized,
                    originalTokenResponse.StatusCode);

                if (refreshResponse.StatusCode == HttpStatusCode.OK)
                {
                    var successor = session with
                    {
                        RefreshCookie = ExtractCookie(
                            refreshResponse, ResetRefreshCookieName)
                    };

                    using var successorResponse = await SendRefreshAsync(
                        client, successor, ct);

                    Assert.Equal(
                        HttpStatusCode.Unauthorized,
                        successorResponse.StatusCode);
                }
            }
            finally
            {
                await CleanupPasswordResetUserAsync(factory, session.UserId);
            }
        }

        [Fact]
        public async Task Register_sends_confirmation_token()
        {
            using var factory = new PortalApiFactory();
            using var client = factory.CreateClient();

            var cancellationToken = TestContext.Current.CancellationToken;

            var email =
                $"user-{Guid.NewGuid():N}@example.com";

            var response = await client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    email,
                    password = "ValidPass123!",
                    fullName = "Test User"
                }, cancellationToken);

            Assert.Equal(
                HttpStatusCode.Accepted,
                response.StatusCode);

            Assert.Contains(
                factory.EmailService.ConfirmationMessages,
                item => item.Email == email);
        }

        [Fact]
        public async Task Resend_confirmation_creates_new_token()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var factory = new PortalApiFactory();
            using var client = factory.CreateClient();

            var email =
                $"user-{Guid.NewGuid():N}@example.com";

            await client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    email,
                    password = "ValidPass123!",
                    fullName = "Test User"
                }, cancellationToken);

            var firstToken =
                factory.EmailService
                    .ConfirmationMessages
                    .Single()
                    .Token;

            var response = await client.PostAsJsonAsync(
                "/api/auth/resend-confirmation",
                new { email },
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.Accepted,
                response.StatusCode);

            var tokens =
                factory.EmailService
                    .ConfirmationMessages
                    .Where(item => item.Email == email)
                    .Select(item => item.Token)
                    .ToList();

            Assert.Equal(2, tokens.Count);
            Assert.NotEqual(firstToken, tokens[1]);
        }

        [Fact]
        public async Task Weak_password_returns_422()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var factory = new PortalApiFactory();
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    email =
                        $"user-{Guid.NewGuid():N}@example.com",
                    password = "weak",
                    fullName = "Test User"
                }, cancellationToken);

            Assert.Equal(
                (HttpStatusCode)422,
                response.StatusCode);
        }

        [Fact]
        public async Task Unknown_email_does_not_reveal_account_existence()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var factory = new PortalApiFactory();
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                "/api/auth/resend-confirmation",
                new
                {
                    email =
                        $"unknown-{Guid.NewGuid():N}@example.com"
                }, cancellationToken);

            Assert.Equal(
                HttpStatusCode.Accepted,
                response.StatusCode);
        }

        [Fact]
        public async Task Reusing_revoked_refresh_token_revokes_entire_family()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var factory = new PortalApiFactory();

            using var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });

            var credentials =
                await SeedConfirmedCitizenAsync(
                    factory,
                    cancellationToken);

            var csrfResponse =
                await client.GetAsync(
                    "/api/security/csrf",
                    cancellationToken);

            Assert.Equal(
                HttpStatusCode.OK,
                csrfResponse.StatusCode);

            var csrfPayload =
                await csrfResponse.Content
                    .ReadFromJsonAsync<CsrfResponse>(
                        cancellationToken);

            var csrfCookie =
                ExtractCookie(
                    csrfResponse,
                    "__Host-portal-csrf");

            var csrfToken =
                csrfPayload!.Token;

            var loginResponse = await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    email = credentials.Email,
                    password = credentials.Password
                }, cancellationToken);

            Assert.Equal(
                HttpStatusCode.OK,
                loginResponse.StatusCode);

            var firstCookie = ExtractCookie(
                loginResponse,
                "__Host-portal-refresh");

            using var refreshRequest =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    "/api/auth/refresh");

            refreshRequest.Headers.Add(
                "Cookie",
                $"{firstCookie}; {csrfCookie}");

            refreshRequest.Headers.Add(
                "X-CSRF-TOKEN",
                csrfToken);

            using var refreshResponse =
                await client.SendAsync(refreshRequest, cancellationToken);

            Assert.Equal(
                HttpStatusCode.OK,
                refreshResponse.StatusCode);

            var secondCookie = ExtractCookie(
                refreshResponse,
                "__Host-portal-refresh");

            using var replayRequest =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    "/api/auth/refresh");

            replayRequest.Headers.Add(
                "Cookie",
                $"{firstCookie}; {csrfCookie}");

            replayRequest.Headers.Add(
                "X-CSRF-TOKEN",
                csrfToken);


            using var replayResponse =
                await client.SendAsync(replayRequest, cancellationToken);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                replayResponse.StatusCode);

            using var familyRequest =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    "/api/auth/refresh");

            familyRequest.Headers.Add(
                "Cookie",
                $"{secondCookie}; {csrfCookie}");

            familyRequest.Headers.Add(
                "X-CSRF-TOKEN",
                csrfToken);

            using var familyResponse =
                await client.SendAsync(familyRequest, cancellationToken);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                familyResponse.StatusCode);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public async Task Refresh_rejects_inactive_or_unconfirmed_user(
            bool isActive,
            bool emailConfirmed)
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            using var factory = new PortalApiFactory();
            using var client = CreateRefreshClient(factory);

            var session = await CreateSessionAsync(
                factory,
                client,
                cancellationToken);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<PortalDbContext>();

                var user = await db.Users.SingleAsync(
                    user => user.id == session.UserId,
                    cancellationToken);

                user.is_active = isActive;
                user.email_confirmed = emailConfirmed;

                await db.SaveChangesAsync(cancellationToken);
            }

            using var response = await SendRefreshAsync(
                client,
                session,
                cancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));

            await using var verificationScope =
                factory.Services.CreateAsyncScope();

            var verificationDb = verificationScope.ServiceProvider
                .GetRequiredService<PortalDbContext>();

            var tokenCount = await verificationDb.RefreshTokens
                .CountAsync(
                    token => token.user_id == session.UserId,
                    cancellationToken);

            Assert.Equal(1, tokenCount);
        }

        [Fact]
        public async Task Parallel_refresh_rotates_once_and_revokes_family()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var connectionString =
                Environment.GetEnvironmentVariable("PORTAL_TEST_CONNECTION");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw SkipException.ForSkip(
                    "PORTAL_TEST_CONNECTION nu este configurată.");
            }

            using var factory = new PortalApiFactory(connectionString);
            using var client = CreateRefreshClient(factory);

            var session = await CreateSessionAsync(
                factory,
                client,
                cancellationToken);

            HttpResponseMessage[] responses = [];

            try
            {
                responses = await Task.WhenAll(
                    Enumerable.Range(0, 10)
                        .Select(_ => SendRefreshAsync(
                            client,
                            session,
                            cancellationToken)));

                Assert.Equal(
                    1,
                    responses.Count(response =>
                        response.StatusCode == HttpStatusCode.OK));

                Assert.Equal(
                    9,
                    responses.Count(response =>
                        response.StatusCode == HttpStatusCode.Unauthorized));

                var successfulResponse = responses.Single(response =>
                    response.StatusCode == HttpStatusCode.OK);

                var successorSession = session with
                {
                    RefreshCookie = ExtractCookie(
                        successfulResponse,
                        "__Host-portal-refresh")
                };

                using var successorResponse = await SendRefreshAsync(
                    client,
                    successorSession,
                    cancellationToken);

                Assert.Equal(
                    HttpStatusCode.Unauthorized,
                    successorResponse.StatusCode);

                await using var scope = factory.Services.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<PortalDbContext>();

                var tokens = await db.RefreshTokens
                    .AsNoTracking()
                    .Where(token => token.user_id == session.UserId)
                    .ToListAsync(cancellationToken);

                Assert.Equal(2, tokens.Count);

                Assert.All(
                    tokens,
                    token => Assert.NotNull(token.revoked_at));
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }

                await using var scope = factory.Services.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<PortalDbContext>();

                await db.RefreshTokens
                    .Where(token => token.user_id == session.UserId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            token => token.replaced_by_id,
                            (Guid?)null),
                        CancellationToken.None);

                await db.Users
                    .Where(user => user.id == session.UserId)
                    .ExecuteDeleteAsync(CancellationToken.None);
            }
        }

        private static string ExtractCookie(
            HttpResponseMessage response,
            string cookieName)
        {
            return response.Headers
                .GetValues("Set-Cookie")
                .Single(cookie =>
                    cookie.StartsWith(
                        cookieName + "=",
                        StringComparison.OrdinalIgnoreCase))
                .Split(';')[0];
        }

        private static async Task<(string Email, string Password)>
        SeedConfirmedCitizenAsync(
            PortalApiFactory factory,
            CancellationToken cancellationToken)
        {
            var email =
                $"citizen-{Guid.NewGuid():N}@example.com";

            const string password = "Citizen123!ChangeMe";

            await using var scope =
                factory.Services.CreateAsyncScope();

            var db =
                scope.ServiceProvider
                    .GetRequiredService<PortalDbContext>();

            var passwordHasher =
                scope.ServiceProvider
                    .GetRequiredService<IPasswordHasher<User>>();

            var user = new User
            {
                id = Guid.NewGuid(),
                email = email,
                full_name = "Test Citizen",
                role = Role.Citizen,
                email_confirmed = true,
                is_active = true
            };

            user.password_hash =
                passwordHasher.HashPassword(user, password);

            db.Users.Add(user);

            await db.SaveChangesAsync(cancellationToken);

            return (email, password);
        }

        private sealed record CsrfResponse(
            string Token);

        private sealed record RefreshTestSession(
            Guid UserId,
            string RefreshCookie,
            string CsrfCookie,
            string CsrfToken);

        private static HttpClient CreateRefreshClient(PortalApiFactory factory)
        {
            return factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });
        }

        private static async Task<RefreshTestSession> CreateSessionAsync(
            PortalApiFactory factory,
            HttpClient client,
            CancellationToken cancellationToken)
        {
            var credentials = await SeedConfirmedCitizenAsync(
                factory,
                cancellationToken);

            using var csrfResponse = await client.GetAsync(
                "/api/security/csrf",
                cancellationToken);

            csrfResponse.EnsureSuccessStatusCode();

            var csrf = await csrfResponse.Content
                .ReadFromJsonAsync<CsrfResponse>(
                    cancellationToken: cancellationToken);

            using var loginResponse = await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    email = credentials.Email,
                    password = credentials.Password
                },
                cancellationToken);

            loginResponse.EnsureSuccessStatusCode();

            await using var scope = factory.Services.CreateAsyncScope();

            var db = scope.ServiceProvider
                .GetRequiredService<PortalDbContext>();

            var userId = await db.Users
                .Where(user => user.email == credentials.Email)
                .Select(user => user.id)
                .SingleAsync(cancellationToken);

            return new RefreshTestSession(
                userId,
                ExtractCookie(loginResponse, "__Host-portal-refresh"),
                ExtractCookie(csrfResponse, "__Host-portal-csrf"),
                csrf!.Token);
        }

        private static async Task<HttpResponseMessage> SendRefreshAsync(
            HttpClient client,
            RefreshTestSession session,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/auth/refresh");

            request.Headers.Add(
                "Cookie",
                $"{session.RefreshCookie}; {session.CsrfCookie}");

            request.Headers.Add("X-CSRF-TOKEN", session.CsrfToken);

            return await client.SendAsync(request, cancellationToken);
        }

        private sealed record PasswordResetToken(Guid Id, string RawToken);

        private static PortalApiFactory CreatePasswordResetFactory()
        {
            var connection = Environment.GetEnvironmentVariable(
                "PORTAL_TEST_CONNECTION");

            if (string.IsNullOrWhiteSpace(connection))
            {
                throw SkipException.ForSkip(
                    "PORTAL_TEST_CONNECTION nu este configurată.");
            }

            return new PortalApiFactory(connection);
        }

        private static async Task<PasswordResetToken> RequestResetTokenAsync(
            PortalApiFactory factory,
            HttpClient client,
            Guid userId,
            CancellationToken ct)
        {
            await using var scope = factory.Services.CreateAsyncScope();

            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();

            var email = await db.Users
                .Where(user => user.id == userId)
                .Select(user => user.email)
                .SingleAsync(ct);

            using var response = await client.PostAsJsonAsync(
                "/api/auth/forgot-password",
                new { email },
                ct);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            var message = Assert.Single(factory.EmailService.ResetMessages, item => item.Email == email);

            var tokenId = await db.AccountTokens
                .Where(token => token.user_id == userId)
                .Select(token => token.id)
                .SingleAsync(ct);

            return new PasswordResetToken(tokenId, message.Token);
        }

        private static Task<HttpResponseMessage> SendPasswordResetAsync(
            HttpClient client,
            string token,
            string password,
            CancellationToken ct)
        {
            return client.PostAsJsonAsync(
                "/api/auth/reset-password",
                new
                {
                    token,
                    newPassword = password,
                    confirmPassword = password
                },
                ct);
        }

        private static async Task<RefreshTestSession> LoginSameUserAsync(
            PortalApiFactory factory,
            HttpClient client,
            RefreshTestSession session,
            string password,
            CancellationToken ct)
        {
            await using var scope = factory.Services.CreateAsyncScope();

            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();

            var email = await db.Users
                .Where(user => user.id == session.UserId)
                .Select(user => user.email)
                .SingleAsync(ct);

            using var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new { email, password },
                ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            return session with
            {
                RefreshCookie = ExtractCookie(response, ResetRefreshCookieName)
            };
        }

        private static async Task AssertResetStateAsync(
            PortalApiFactory factory,
            Guid userId,
            Guid resetTokenId,
            string expectedPassword,
            CancellationToken ct)
        {
            await using var scope = factory.Services.CreateAsyncScope();

            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();

            var hasher = scope.ServiceProvider
                .GetRequiredService<IPasswordHasher<User>>();

            var user = await db.Users
                .AsNoTracking()
                .SingleAsync(user => user.id == userId, ct);

            Assert.NotEqual(
                PasswordVerificationResult.Failed,
                hasher.VerifyHashedPassword(
                    user, user.password_hash, expectedPassword));

            Assert.Equal(
                PasswordVerificationResult.Failed,
                hasher.VerifyHashedPassword(
                    user, user.password_hash, ResetSeedPassword));

            var resetToken = await db.AccountTokens
                .AsNoTracking()
                .SingleAsync(token =>
                    token.id == resetTokenId && token.user_id == userId,
                    ct);

            Assert.NotNull(resetToken.consumed_at);

            var refreshTokens = await db.RefreshTokens
                .AsNoTracking()
                .Where(token => token.user_id == userId)
                .ToListAsync(ct);

            Assert.NotEmpty(refreshTokens);
            Assert.All(refreshTokens, token => Assert.NotNull(token.revoked_at));
        }

        private static async Task AssertResetRejectedAsync(
            HttpResponseMessage response,
            CancellationToken ct)
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(ct));

            var problem = json.RootElement;

            Assert.Equal(422, problem.GetProperty("status").GetInt32());
            Assert.Equal(
                "/api/auth/reset-password",
                problem.GetProperty("instance").GetString());

            var traceId = problem.GetProperty("traceId").GetString();

            Assert.False(string.IsNullOrWhiteSpace(traceId));
            Assert.Equal(
                traceId,
                response.Headers.GetValues("X-Trace-Id").Single());
        }

        private static async Task CleanupPasswordResetUserAsync(
            PortalApiFactory factory,
            Guid userId)
        {
            await using var scope = factory.Services.CreateAsyncScope();

            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();

            await db.RefreshTokens
                .Where(token => token.user_id == userId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        token => token.replaced_by_id,
                        (Guid?)null),
                    CancellationToken.None);

            await db.Users
                .Where(user => user.id == userId)
                .ExecuteDeleteAsync(CancellationToken.None);
        }

    }
}
