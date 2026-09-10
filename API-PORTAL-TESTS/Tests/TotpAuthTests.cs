using API_PORTAL.Data;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using Microsoft.AspNetCore.Identity;
using API_PORTAL_TESTS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Xunit;

namespace API_PORTAL_TESTS.Tests;

public sealed class TotpAuthTests : IClassFixture<PortalApiFactory>
{
    private readonly PortalApiFactory factory;
    private readonly HttpClient client;
    private readonly string adminEmail =
        $"totp-admin-{Guid.NewGuid():N}@portal.test";

    public TotpAuthTests(PortalApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_totp_uses_challenge_and_recovery_code_only_once()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateAdminAsync(cancellationToken);

        using var firstLogin = await LoginAsync(cancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstLogin.StatusCode);
        var tokens = await firstLogin.Content.ReadFromJsonAsync<TokenResponse>(
            cancellationToken);
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        using var setup = await client.PostAsync(
            "/api/auth/totp/setup", null, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var enrollment = await setup.Content.ReadFromJsonAsync<TotpSetupResponse>(
            cancellationToken);
        Assert.NotNull(enrollment);
        Assert.StartsWith("otpauth://totp/", enrollment!.OtpAuthUri);

        using var enable = await client.PostAsJsonAsync(
            "/api/auth/totp/enable",
            new { code = GenerateCurrentCode(enrollment.Secret) },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        var enabled = await enable.Content.ReadFromJsonAsync<TotpEnabledResponse>(
            cancellationToken);
        var recoveryCode = Assert.Single(enabled!.RecoveryCodes.Take(1));

        client.DefaultRequestHeaders.Authorization = null;
        using var challengeResponse = await LoginAsync(cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, challengeResponse.StatusCode);
        var challenge = await challengeResponse.Content
            .ReadFromJsonAsync<LoginTotpChallengeResponse>(cancellationToken);
        Assert.NotNull(challenge);

        using var invalidCode = await client.PostAsJsonAsync(
            "/api/auth/totp/verify-login",
            new { challenge = challenge!.Challenge, code = "invalid-code" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidCode.StatusCode);

        using var verified = await client.PostAsJsonAsync(
            "/api/auth/totp/verify-login",
            new { challenge = challenge!.Challenge, code = recoveryCode },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);

        using var replayChallengeResponse = await LoginAsync(cancellationToken);
        var replayChallenge = await replayChallengeResponse.Content
            .ReadFromJsonAsync<LoginTotpChallengeResponse>(cancellationToken);
        using var replay = await client.PostAsJsonAsync(
            "/api/auth/totp/verify-login",
            new { challenge = replayChallenge!.Challenge, code = recoveryCode },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, replay.StatusCode);
    }

    private async Task CreateAdminAsync(CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        var admin = new User
        {
            id = Guid.NewGuid(),
            email = adminEmail,
            full_name = "Portal TOTP Test Admin",
            role = Role.Admin,
            email_confirmed = true,
            is_active = true
        };
        var passwordHasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<User>>();
        admin.password_hash = passwordHasher.HashPassword(
            admin, "Admin123!ChangeMe");
        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);
    }

    private Task<HttpResponseMessage> LoginAsync(CancellationToken cancellationToken) =>
        client.PostAsJsonAsync("/api/auth/login", new
        {
            email = adminEmail,
            password = "Admin123!ChangeMe"
        }, cancellationToken);

    private static string GenerateCurrentCode(string secret)
    {
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, counter);
        using var hmac = new HMACSHA1(Base32Decode(secret));
        var hash = hmac.ComputeHash(bytes.ToArray());
        var offset = hash[^1] & 0x0f;
        var value = ((hash[offset] & 0x7f) << 24) |
                    (hash[offset + 1] << 16) |
                    (hash[offset + 2] << 8) |
                    hash[offset + 3];
        return (value % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string value)
    {
        var buffer = 0;
        var bits = 0;
        var bytes = new List<byte>();
        foreach (var character in value)
        {
            var index = character is >= 'A' and <= 'Z'
                ? character - 'A'
                : character - '2' + 26;
            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return bytes.ToArray();
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = null!;
    }

    private sealed class TotpSetupResponse
    {
        [JsonPropertyName("secret")]
        public string Secret { get; set; } = null!;

        [JsonPropertyName("otpAuthUri")]
        public string OtpAuthUri { get; set; } = null!;
    }

    private sealed class TotpEnabledResponse
    {
        [JsonPropertyName("recoveryCodes")]
        public List<string> RecoveryCodes { get; set; } = [];
    }

    private sealed class LoginTotpChallengeResponse
    {
        [JsonPropertyName("challenge")]
        public string Challenge { get; set; } = null!;
    }
}
