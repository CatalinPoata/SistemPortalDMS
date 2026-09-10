using Microsoft.AspNetCore.DataProtection;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace API_PORTAL.Auth;

public sealed record TotpEnrollment(
    string ProtectedSecret,
    string Secret,
    string OtpAuthUri);

public interface ITotpService
{
    TotpEnrollment CreateEnrollment(Guid userId, string email);
    string UnprotectSecret(string protectedSecret);
    bool TryVerify(string secret, string code, long? lastUsedCounter,
        out long matchedCounter);
    IReadOnlyList<string> CreateRecoveryCodes(int count = 10);
    string HashRecoveryCode(string code);
    string CreateLoginChallenge(Guid userId);
    bool TryReadLoginChallenge(string challenge, out Guid userId);
}

public sealed class TotpService : ITotpService
{
    private const int SecretBytes = 20;
    private const int PeriodSeconds = 30;
    private const int Digits = 6;

    private readonly IDataProtector secretProtector;
    private readonly ITimeLimitedDataProtector challengeProtector;

    public TotpService(IDataProtectionProvider dataProtectionProvider)
    {
        secretProtector = dataProtectionProvider.CreateProtector(
            "API-PORTAL.Auth.Totp.Secret.v1");
        challengeProtector = dataProtectionProvider.CreateProtector(
                "API-PORTAL.Auth.Totp.LoginChallenge.v1")
            .ToTimeLimitedDataProtector();
    }

    public TotpEnrollment CreateEnrollment(Guid userId, string email)
    {
        var secret = Base32Encode(RandomNumberGenerator.GetBytes(SecretBytes));
        var label = Uri.EscapeDataString($"Portal:{email}");
        var issuer = Uri.EscapeDataString("Portal Servicii");
        var otpAuthUri =
            $"otpauth://totp/{label}?secret={secret}&issuer={issuer}" +
            $"&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}";

        return new TotpEnrollment(
            secretProtector.Protect(secret), secret, otpAuthUri);
    }

    public string UnprotectSecret(string protectedSecret) =>
        secretProtector.Unprotect(protectedSecret);

    public bool TryVerify(string secret, string code, long? lastUsedCounter,
        out long matchedCounter)
    {
        matchedCounter = 0;
        var normalizedCode = NormalizeCode(code);
        if (normalizedCode.Length != Digits ||
            !normalizedCode.All(char.IsAsciiDigit)) return false;

        byte[] secretBytes;
        try { secretBytes = Base32Decode(secret); }
        catch (FormatException) { return false; }

        var currentCounter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() /
            PeriodSeconds;

        for (var offset = -1; offset <= 1; offset++)
        {
            var counter = currentCounter + offset;
            if (lastUsedCounter.HasValue && counter <= lastUsedCounter) continue;

            var expected = GenerateCode(secretBytes, counter);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected),
                    Encoding.ASCII.GetBytes(normalizedCode)))
            {
                matchedCounter = counter;
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<string> CreateRecoveryCodes(int count = 10) =>
        Enumerable.Range(0, count)
            .Select(_ =>
            {
                var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
                return $"{raw[..5]}-{raw[5..]}";
            })
            .ToArray();

    public string HashRecoveryCode(string code) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(NormalizeCode(code)))).ToLowerInvariant();

    public string CreateLoginChallenge(Guid userId) =>
        challengeProtector.Protect(userId.ToString("D"), TimeSpan.FromMinutes(5));

    public bool TryReadLoginChallenge(string challenge, out Guid userId)
    {
        userId = Guid.Empty;
        try
        {
            var raw = challengeProtector.Unprotect(challenge, out _);
            return Guid.TryParse(raw, out userId);
        }
        catch (CryptographicException) { return false; }
    }

    private static string GenerateCode(byte[] secret, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);
        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) |
                     (hash[offset + 1] << 16) |
                     (hash[offset + 2] << 8) |
                     hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static string NormalizeCode(string code) =>
        code.Trim().Replace("-", string.Empty).Replace(" ", string.Empty)
            .ToUpperInvariant();

    private static string Base32Encode(ReadOnlySpan<byte> bytes)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new StringBuilder((bytes.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0) output.Append(alphabet[(buffer << (5 - bits)) & 31]);
        return output.ToString();
    }

    private static byte[] Base32Decode(string value)
    {
        var buffer = 0;
        var bits = 0;
        var bytes = new List<byte>();
        foreach (var character in value.Trim().TrimEnd('=').ToUpperInvariant())
        {
            var index = character switch
            {
                >= 'A' and <= 'Z' => character - 'A',
                >= '2' and <= '7' => character - '2' + 26,
                _ => -1
            };
            if (index < 0) throw new FormatException("Secret TOTP invalid.");
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
}
