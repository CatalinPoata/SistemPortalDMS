using API_DMS.Data;
using API_DMS.Entities;
using API_DMS.Entities.Base;
using API_DMS_TESTS.Infrastructure;
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
using Task = System.Threading.Tasks.Task;

namespace API_DMS_TESTS.Tests
{
    public sealed partial class AccountAuthTests
    {
        private const string ResetSeedPassword = "Clerk123!ChangeMe";
        private const string ResetNewPassword = "NewPassword123!ChangeMe";
        private const string ResetOtherPassword = "OtherPassword456!ChangeMe";
        private const string ResetRefreshCookieName = "__Host-dms-refresh";

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
                        .GetRequiredService<DmsDbContext>();

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
                    .GetRequiredService<DmsDbContext>();

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
        public async Task Unknown_email_does_not_reveal_account_existence()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var factory = new DmsApiFactory();
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
        public async Task Seeded_clerk_can_login()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var factory = new DmsApiFactory();
            using var client = factory.CreateClient();

            var email = $"clerk-{Guid.NewGuid():N}@example.com";

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<DmsDbContext>();

                var hasher = scope.ServiceProvider
                    .GetRequiredService<IPasswordHasher<User>>();

                var user = new User
                {
                    id = Guid.NewGuid(),
                    email = email,
                    full_name = "Test Clerk",
                    role = Role.Clerk,
                    email_confirmed = true,
                    is_active = true
                };

                user.password_hash =
                    hasher.HashPassword(user, "Clerk123!ChangeMe");

                db.Users.Add(user);
                await db.SaveChangesAsync(cancellationToken);
            }

            var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    email,
                    password = "Clerk123!ChangeMe"
                }, cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(
                response.Headers.Contains("Set-Cookie"));
        }

        [Fact]
        public async Task Reusing_revoked_refresh_token_revokes_entire_family()
        {
            var cancellationToken = TestContext.Current.CancellationToken;


            using var factory = new DmsApiFactory();

            using var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });

            var credentials =
                await SeedConfirmedClerkAsync(
                    factory,
                    cancellationToken);

            var csrfResponse =
                await client.GetAsync(
                    "/api/security/csrf",
                    cancellationToken);

            Assert.Equal(
                HttpStatusCode.OK,
                csrfResponse.StatusCode);

            var csrfBody =
                await csrfResponse.Content.ReadAsStringAsync(
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
                    "__Host-dms-csrf");

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
                "__Host-dms-refresh");

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
                await client.SendAsync(
                    refreshRequest,
                    cancellationToken);

            var refreshBody =
                await refreshResponse.Content.ReadAsStringAsync(
                    cancellationToken);

            Assert.True(
                refreshResponse.IsSuccessStatusCode,
                $"Refresh returned " +
                $"{(int)refreshResponse.StatusCode}: " +
                refreshBody);

            var secondCookie = ExtractCookie(
                refreshResponse,
                "__Host-dms-refresh");

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

            using var factory = new DmsApiFactory();
            using var client = CreateRefreshClient(factory);

            var session = await CreateSessionAsync(
                factory,
                client,
                cancellationToken);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<DmsDbContext>();

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
                .GetRequiredService<DmsDbContext>();

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
                Environment.GetEnvironmentVariable("DMS_TEST_CONNECTION");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw SkipException.ForSkip(
                    "DMS_TEST_CONNECTION nu este configurată.");
            }

            using var factory = new DmsApiFactory(connectionString);
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
                        "__Host-dms-refresh")
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
                    .GetRequiredService<DmsDbContext>();

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
                    .GetRequiredService<DmsDbContext>();

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

        private static async Task<(string Email, string Password)>
            SeedConfirmedClerkAsync(
                DmsApiFactory factory,
                CancellationToken cancellationToken)
        {
            var email =
                $"clerk-{Guid.NewGuid():N}@example.com";

            const string password = "Clerk123!ChangeMe";

            await using var scope =
                factory.Services.CreateAsyncScope();

            var db =
                scope.ServiceProvider
                    .GetRequiredService<DmsDbContext>();

            var passwordHasher =
                scope.ServiceProvider
                    .GetRequiredService<IPasswordHasher<User>>();

            var user = new User
            {
                id = Guid.NewGuid(),
                email = email,
                full_name = "Test Clerk",
                role = Role.Clerk,
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

        private sealed record RefreshTestSession(
            Guid UserId,
            string RefreshCookie,
            string CsrfCookie,
            string CsrfToken);

        private static HttpClient CreateRefreshClient(DmsApiFactory factory)
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
            DmsApiFactory factory,
            HttpClient client,
            CancellationToken cancellationToken)
        {
            var credentials = await SeedConfirmedClerkAsync(
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

            Assert.True(
                loginResponse.IsSuccessStatusCode,
                $"Login-ul de test a răspuns {(int)loginResponse.StatusCode}: " +
                await loginResponse.Content.ReadAsStringAsync(cancellationToken));

            await using var scope = factory.Services.CreateAsyncScope();

            var db = scope.ServiceProvider
                .GetRequiredService<DmsDbContext>();

            var userId = await db.Users
                .Where(user => user.email == credentials.Email)
                .Select(user => user.id)
                .SingleAsync(cancellationToken);

            return new RefreshTestSession(
                userId,
                ExtractCookie(loginResponse, "__Host-dms-refresh"),
                ExtractCookie(csrfResponse, "__Host-dms-csrf"),
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

        private static DmsApiFactory CreatePasswordResetFactory()
        {
            var connection = Environment.GetEnvironmentVariable(
                "DMS_TEST_CONNECTION");

            if (string.IsNullOrWhiteSpace(connection))
            {
                throw SkipException.ForSkip(
                    "DMS_TEST_CONNECTION nu este configurată.");
            }

            return new DmsApiFactory(connection);
        }

        private static async Task<PasswordResetToken> RequestResetTokenAsync(
            DmsApiFactory factory,
            HttpClient client,
            Guid userId,
            CancellationToken ct)
        {
            await using var scope = factory.Services.CreateAsyncScope();

            var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();

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
            DmsApiFactory factory,
            HttpClient client,
            RefreshTestSession session,
            string password,
            CancellationToken ct)
        {
            await using var scope = factory.Services.CreateAsyncScope();

            var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();

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
            DmsApiFactory factory,
            Guid userId,
            Guid resetTokenId,
            string expectedPassword,
            CancellationToken ct)
        {
            await using var scope = factory.Services.CreateAsyncScope();

            var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();

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
            DmsApiFactory factory,
            Guid userId)
        {
            await using var scope = factory.Services.CreateAsyncScope();

            var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();

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
