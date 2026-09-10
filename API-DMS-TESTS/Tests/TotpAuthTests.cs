using API_DMS.Data;
using API_DMS.Seed;
using API_DMS_TESTS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Xunit;

namespace API_DMS_TESTS.Tests;

public sealed class TotpAuthTests : IClassFixture<DmsApiFactory>
{
    private readonly DmsApiFactory factory;
    private readonly HttpClient client;

    public TotpAuthTests(DmsApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Totp_login_uses_challenge_and_recovery_code_only_once()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await EnsureSeedAsync(cancellationToken);

        var firstLogin = await LoginAsync(cancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstLogin.StatusCode);

        var tokens = await firstLogin.Content.ReadFromJsonAsync<TokenResponse>(
            cancellationToken);
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        using var setup = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/totp/setup");
        using var setupResponse = await client.SendAsync(setup, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);

        var enrollment = await setupResponse.Content
            .ReadFromJsonAsync<TotpSetupResponse>(cancellationToken);
        Assert.NotNull(enrollment);
        Assert.StartsWith("otpauth://totp/", enrollment!.OtpAuthUri);

        using var enableResponse = await client.PostAsJsonAsync(
            "/api/auth/totp/enable",
            new { code = GenerateCurrentCode(enrollment.Secret) },
            cancellationToken);

        Assert.True(
            enableResponse.StatusCode == HttpStatusCode.OK,
            $"Activarea TOTP a răspuns {(int)enableResponse.StatusCode}: " +
            await enableResponse.Content.ReadAsStringAsync(cancellationToken));

        var enabled = await enableResponse.Content
            .ReadFromJsonAsync<TotpEnabledResponse>(cancellationToken);
        Assert.NotNull(enabled);
        var recoveryCode = Assert.Single(enabled!.RecoveryCodes.Take(1));

        using var challengeResponse = await LoginAsync(cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, challengeResponse.StatusCode);

        var challenge = await challengeResponse.Content
            .ReadFromJsonAsync<LoginChallengeResponse>(cancellationToken);
        Assert.NotNull(challenge);
        Assert.False(string.IsNullOrWhiteSpace(challenge!.Challenge));

        using var invalidCodeResponse = await client.PostAsJsonAsync(
            "/api/auth/totp/verify-login",
            new { challenge = challenge.Challenge, code = "invalid-code" },
            cancellationToken);
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            invalidCodeResponse.StatusCode);

        using var verifiedResponse = await client.PostAsJsonAsync(
            "/api/auth/totp/verify-login",
            new { challenge = challenge.Challenge, code = recoveryCode },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, verifiedResponse.StatusCode);

        using var secondChallengeResponse = await LoginAsync(cancellationToken);
        var secondChallenge = await secondChallengeResponse.Content
            .ReadFromJsonAsync<LoginChallengeResponse>(cancellationToken);

        using var reusedCodeResponse = await client.PostAsJsonAsync(
            "/api/auth/totp/verify-login",
            new { challenge = secondChallenge!.Challenge, code = recoveryCode },
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            reusedCodeResponse.StatusCode);
    }

    private async Task<HttpResponseMessage> LoginAsync(
        CancellationToken cancellationToken)
    {
        return await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "clerk2@example.com",
                password = "Clerk123!ChangeMe"
            },
            cancellationToken);
    }

    private async Task EnsureSeedAsync(CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await DmsDbSeeder.SeedAsync(scope.ServiceProvider, cancellationToken);

        var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
        var user = await db.Users.SingleAsync(
            item => item.email == "clerk2@example.com",
            cancellationToken);
        var existing = await db.UserTotps.SingleOrDefaultAsync(
            item => item.user_id == user.id,
            cancellationToken);

        if (existing is not null)
        {
            db.UserTotps.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

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

    private sealed class LoginChallengeResponse
    {
        [JsonPropertyName("challenge")]
        public string Challenge { get; set; } = null!;
    }
}
