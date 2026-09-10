namespace API_DMS.DTO.Auth;

public sealed record TotpSetupResponse(
    string Secret,
    string OtpAuthUri);

public sealed record TotpEnabledResponse(
    IReadOnlyList<string> RecoveryCodes);

public sealed record LoginTotpChallengeResponse(
    string Challenge,
    DateTimeOffset ExpiresAt);
