using API_DMS.Auth;
using API_DMS.Data;
using API_DMS.DTO.Auth;
using API_DMS.Email;
using API_DMS.Entities;
using API_DMS.Entities.Base;
using API_DMS.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace API_DMS.Controllers.Auth
{
    [ApiController]
    [Route("api/auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly DmsDbContext db;
        private readonly IPasswordHasher<User> passwordHasher;
        private readonly IJwtTokenService tokenService;

        private readonly IAccountTokenService accountTokenService;
        private readonly IHostEnvironment environment;
        private readonly IAccountEmailService accountEmailService;
        private readonly IRefreshSessionLock refreshSessionLock;
        private readonly ITotpService totpService;

        public AuthController(
            DmsDbContext db,
            IPasswordHasher<User> passwordHasher,
            IJwtTokenService tokenService,
            IAccountTokenService accountTokenService,
            IHostEnvironment environment,
            IAccountEmailService accountEmailService,
            IRefreshSessionLock refreshSessionLock,
            ITotpService totpService)
        {
            this.db = db;
            this.passwordHasher = passwordHasher;
            this.tokenService = tokenService;
            this.accountTokenService = accountTokenService;
            this.environment = environment;
            this.accountEmailService = accountEmailService;
            this.refreshSessionLock = refreshSessionLock;
            this.totpService = totpService;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<TokenResponse>> Login(
            LoginRequest request,
            CancellationToken cancellationToken)
        {
            var email = request.Email
                .Trim()
                .ToLowerInvariant();

            var user = await db.Users
                .SingleOrDefaultAsync(
                    u => u.email == email,
                    cancellationToken);

            var now = DateTimeOffset.UtcNow;

            if (user is null ||
                !user.is_active ||
                !user.email_confirmed ||
                user.lockout_end > now)
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Autentificare eșuată",
                    Detail = "Emailul sau parola sunt incorecte."
                });
            }

            var verificationResult =
                passwordHasher.VerifyHashedPassword(
                    user,
                    user.password_hash,
                    request.Password);

            if (verificationResult ==
                PasswordVerificationResult.Failed)
            {
                user.failed_login_count++;

                if (user.failed_login_count >= 5)
                {
                    user.lockout_end = now.AddMinutes(15);
                }

                await db.SaveChangesAsync(cancellationToken);

                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Autentificare eșuată",
                    Detail = "Emailul sau parola sunt incorecte."
                });
            }

            if (verificationResult ==
                PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.password_hash =
                    passwordHasher.HashPassword(
                        user,
                        request.Password);
            }

            var userTotp = await db.UserTotps
                .SingleOrDefaultAsync(
                    item => item.user_id == user.id && item.is_enabled,
                    cancellationToken);

            if (userTotp is not null)
            {
                await db.SaveChangesAsync(cancellationToken);

                return Accepted(new LoginTotpChallengeResponse(
                    totpService.CreateLoginChallenge(user.id),
                    DateTimeOffset.UtcNow.AddMinutes(5)));
            }

            user.failed_login_count = 0;
            user.lockout_end = null;

            return Ok(await IssueTokensAsync(user, cancellationToken));
        }

        [AllowAnonymous]
        [HttpPost("totp/verify-login")]
        public async Task<ActionResult<TokenResponse>> VerifyTotpLogin(
            VerifyTotpLoginRequest request,
            CancellationToken cancellationToken)
        {
            if (!totpService.TryReadLoginChallenge(
                    request.Challenge,
                    out var userId))
            {
                return Unauthorized(InvalidLogin());
            }

            await using var transaction = await refreshSessionLock.AcquireAsync(
                userId,
                cancellationToken);

            var user = await db.Users.SingleOrDefaultAsync(
                item => item.id == userId,
                cancellationToken);

            var userTotp = await db.UserTotps.SingleOrDefaultAsync(
                item => item.user_id == userId && item.is_enabled,
                cancellationToken);

            if (user is null || userTotp is null ||
                !user.is_active || !user.email_confirmed ||
                user.lockout_end > DateTimeOffset.UtcNow)
            {
                return Unauthorized(InvalidLogin());
            }

            var secret = totpService.UnprotectSecret(
                userTotp.protected_secret);

            var verified = totpService.TryVerify(
                secret,
                request.Code,
                userTotp.last_used_counter,
                out var counter);

            if (verified)
            {
                userTotp.last_used_counter = counter;
            }
            else
            {
                verified = TryConsumeRecoveryCode(userTotp, request.Code);
            }

            if (!verified)
            {
                await RegisterFailedLoginAsync(user, cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
                return UnprocessableEntity(InvalidTotpCode());
            }

            user.failed_login_count = 0;
            user.lockout_end = null;

            var response = await IssueTokensAsync(user, cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return Ok(response);
        }

        [Authorize(Roles = "Clerk,Admin")]
        [HttpPost("totp/setup")]
        public async Task<ActionResult<TotpSetupResponse>> SetupTotp(
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();

            if (userId is null)
            {
                return Unauthorized();
            }

            var user = await db.Users.SingleOrDefaultAsync(
                item => item.id == userId.Value && item.is_active,
                cancellationToken);

            if (user is null)
            {
                return Unauthorized();
            }

            var existing = await db.UserTotps.SingleOrDefaultAsync(
                item => item.user_id == user.id,
                cancellationToken);

            if (existing?.is_enabled == true)
            {
                return Conflict(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    "TOTP este deja activ",
                    "Dezactivează autentificarea în doi pași înainte de a o configura din nou."));
            }

            var enrollment = totpService.CreateEnrollment(user.id, user.email);

            if (existing is null)
            {
                existing = new UserTotp
                {
                    id = Guid.NewGuid(),
                    user_id = user.id
                };

                db.UserTotps.Add(existing);
            }

            existing.protected_secret = enrollment.ProtectedSecret;
            existing.is_enabled = false;
            existing.last_used_counter = null;
            existing.enabled_at = null;
            existing.recovery_code_hashes = JsonDocument.Parse("[]");

            await db.SaveChangesAsync(cancellationToken);

            return Ok(new TotpSetupResponse(
                enrollment.Secret,
                enrollment.OtpAuthUri));
        }

        [Authorize(Roles = "Clerk,Admin")]
        [HttpPost("totp/enable")]
        public async Task<ActionResult<TotpEnabledResponse>> EnableTotp(
            EnableTotpRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();

            if (userId is null)
            {
                return Unauthorized();
            }

            var userTotp = await db.UserTotps.SingleOrDefaultAsync(
                item => item.user_id == userId.Value && !item.is_enabled,
                cancellationToken);

            if (userTotp is null)
            {
                return UnprocessableEntity(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Configurare TOTP indisponibilă",
                    "Începe mai întâi configurarea autentificării în doi pași."));
            }

            var secret = totpService.UnprotectSecret(
                userTotp.protected_secret);

            if (!totpService.TryVerify(
                    secret,
                    request.Code,
                    null,
                    out var counter))
            {
                return UnprocessableEntity(InvalidTotpCode());
            }

            var recoveryCodes = totpService.CreateRecoveryCodes();
            var recoveryHashes = recoveryCodes
                .Select(totpService.HashRecoveryCode)
                .ToArray();

            userTotp.last_used_counter = counter;
            userTotp.recovery_code_hashes = JsonDocument.Parse(
                JsonSerializer.Serialize(recoveryHashes));
            userTotp.is_enabled = true;
            userTotp.enabled_at = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            return Ok(new TotpEnabledResponse(recoveryCodes));
        }

        [Authorize(Roles = "Clerk,Admin")]
        [HttpPost("totp/disable")]
        public async Task<IActionResult> DisableTotp(
            DisableTotpRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();

            if (userId is null)
            {
                return Unauthorized();
            }

            var user = await db.Users.SingleOrDefaultAsync(
                item => item.id == userId.Value,
                cancellationToken);

            var userTotp = await db.UserTotps.SingleOrDefaultAsync(
                item => item.user_id == userId.Value && item.is_enabled,
                cancellationToken);

            if (user is null || userTotp is null ||
                passwordHasher.VerifyHashedPassword(
                    user,
                    user.password_hash,
                    request.CurrentPassword) == PasswordVerificationResult.Failed)
            {
                return UnprocessableEntity(InvalidTotpCode());
            }

            var secret = totpService.UnprotectSecret(
                userTotp.protected_secret);

            var verified = totpService.TryVerify(
                secret,
                request.Code,
                userTotp.last_used_counter,
                out _);

            if (!verified)
            {
                verified = TryConsumeRecoveryCode(userTotp, request.Code);
            }

            if (!verified)
            {
                return UnprocessableEntity(InvalidTotpCode());
            }

            db.UserTotps.Remove(userTotp);
            await db.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<TokenResponse>> Refresh(
    CancellationToken cancellationToken)
        {
            if (!Request.Cookies.TryGetValue(
                    AuthCookieOptions.RefreshTokenName,
                    out var rawRefreshToken) ||
                string.IsNullOrWhiteSpace(rawRefreshToken))
            {
                return Unauthorized(InvalidRefreshToken());
            }

            var tokenHash = tokenService.HashRefreshToken(rawRefreshToken);

            var userId = await db.RefreshTokens
                .AsNoTracking()
                .Where(token => token.token_hash == tokenHash)
                .Select(token => (Guid?)token.user_id)
                .SingleOrDefaultAsync(cancellationToken);

            if (userId is null)
            {
                return Unauthorized(InvalidRefreshToken());
            }

            await using var transaction =
                await refreshSessionLock.AcquireAsync(
                    userId.Value,
                    cancellationToken);

            var currentToken = await db.RefreshTokens
                .Include(token => token.user)
                .SingleOrDefaultAsync(
                    token => token.token_hash == tokenHash,
                    cancellationToken);

            var now = DateTimeOffset.UtcNow;

            if (currentToken is null)
            {
                return Unauthorized(InvalidRefreshToken());
            }

            if (currentToken.revoked_at is not null)
            {
                await RevokeTokenFamilyAsync(
                    currentToken.user_id,
                    currentToken.id,
                    now,
                    cancellationToken);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return Unauthorized(InvalidRefreshToken());
            }

            var user = currentToken.user;

            if (currentToken.expires_at <= now ||
                user is null ||
                !user.is_active ||
                !user.email_confirmed)
            {
                return Unauthorized(InvalidRefreshToken());
            }

            var issuedTokens = tokenService.Create(user);
            var nextToken = issuedTokens.RefreshTokenEntity;

            nextToken.user = user;

            currentToken.revoked_at = now;
            currentToken.replaced_by_id = nextToken.id;

            db.RefreshTokens.Add(nextToken);

            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            Response.Cookies.Append(
                AuthCookieOptions.RefreshTokenName,
                issuedTokens.RefreshToken,
                AuthCookieOptions.Create(nextToken.expires_at));

            return Ok(new TokenResponse(
                issuedTokens.AccessToken,
                issuedTokens.AccessTokenExpiresAt));
        }

        [Authorize]
        [HttpPost("revoke")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();

            if (userId is null)
            {
                return Unauthorized();
            }

            await using var transaction =
                await refreshSessionLock.AcquireAsync(
                    userId.Value,
                    cancellationToken);

            if (Request.Cookies.TryGetValue(
                    AuthCookieOptions.RefreshTokenName,
                    out var rawRefreshToken) &&
                !string.IsNullOrWhiteSpace(rawRefreshToken))
            {
                var tokenHash =
                    tokenService.HashRefreshToken(rawRefreshToken);

                var refreshToken =
                    await db.RefreshTokens
                        .SingleOrDefaultAsync(
                            token =>
                                token.token_hash == tokenHash &&
                                token.user_id == userId.Value,
                            cancellationToken);

                if (refreshToken is not null)
                {
                    await RevokeTokenFamilyAsync(
                        userId.Value,
                        refreshToken.id,
                        DateTimeOffset.UtcNow,
                        cancellationToken);
                }
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            Response.Cookies.Delete(
                AuthCookieOptions.RefreshTokenName,
                AuthCookieOptions.CreateDeleteOptions());

            return NoContent();
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<CurrentUserResponse>> Me(
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();

            if (userId is null)
            {
                return Unauthorized();
            }

            var user = await db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    u => u.id == userId.Value,
                    cancellationToken);

            if (user is null ||
                !user.is_active ||
                !user.email_confirmed)
            {
                return Unauthorized();
            }

            return Ok(new CurrentUserResponse(
                user.id,
                user.email,
                user.full_name,
                user.role.ToString()));
        }

        [AllowAnonymous]
        [HttpPost("confirm-email")]
        public async Task<ActionResult<ConfirmEmailResponse>> ConfirmEmail(
            ConfirmEmailRequest request,
            CancellationToken cancellationToken)
        {
            var tokenHash =
                accountTokenService.Hash(
                    request.Token.Trim());

            var now = DateTimeOffset.UtcNow;

            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    cancellationToken);

            var accountToken = await db.AccountTokens
                .Include(token => token.user)
                .SingleOrDefaultAsync(
                    token =>
                        token.token_hash == tokenHash &&
                        token.purpose ==
                            AccountTokenPurpose.EmailConfirmation &&
                        token.consumed_at == null &&
                        token.expires_at > now,
                    cancellationToken);

            if (accountToken is null ||
                accountToken.user is null ||
                !accountToken.user.is_active)
            {
                return UnprocessableEntity(
                    InvalidConfirmationToken());
            }

            var consumedRows = await db.AccountTokens
                .Where(token =>
                    token.id == accountToken.id &&
                    token.consumed_at == null &&
                    token.expires_at > now)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            token => token.consumed_at,
                            now),
                    cancellationToken);

            if (consumedRows != 1)
            {
                return UnprocessableEntity(
                    InvalidConfirmationToken());
            }

            accountToken.consumed_at = now;
            accountToken.user.email_confirmed = true;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new ConfirmEmailResponse(
                "Adresa de email a fost confirmată."));
        }

        [AllowAnonymous]
        [HttpPost("forgot-password")]
        public async Task<ActionResult<ForgotPasswordResponse>>
        ForgotPassword(
            ForgotPasswordRequest request,
            CancellationToken cancellationToken)
        {
            var message =
                "Dacă adresa există, vei primi instrucțiuni pentru resetarea parolei.";

            var email = request.Email
                .Trim()
                .ToLowerInvariant();

            var user = await db.Users
                .SingleOrDefaultAsync(
                    item => item.email == email,
                    cancellationToken);

            if (user is null ||
                !user.is_active ||
                !user.email_confirmed)
            {
                return Accepted(new ForgotPasswordResponse(
                    message,
                    null));
            }

            var now = DateTimeOffset.UtcNow;

            var previousTokens = await db.AccountTokens
                .Where(token =>
                    token.user_id == user.id &&
                    token.purpose ==
                        AccountTokenPurpose.PasswordReset &&
                    token.consumed_at == null)
                .ToListAsync(cancellationToken);

            foreach (var previousToken in previousTokens)
            {
                previousToken.consumed_at = now;
            }

            var generatedToken =
                accountTokenService.Create(
                    user.id,
                    AccountTokenPurpose.PasswordReset,
                    TimeSpan.FromHours(1));

            db.AccountTokens.Add(generatedToken.Entity);

            await db.SaveChangesAsync(cancellationToken);

            await accountEmailService.SendPasswordResetAsync(
                user,
                generatedToken.RawToken,
                cancellationToken);

            return Accepted(new ForgotPasswordResponse(
                message,
                environment.IsDevelopment()
                    ? generatedToken.RawToken
                    : null));
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<ActionResult<ResetPasswordResponse>>
        ResetPassword(
            ResetPasswordRequest request,
            CancellationToken cancellationToken)
        {
            var tokenHash = accountTokenService.Hash(request.Token.Trim());

            var userId = await db.AccountTokens
                .AsNoTracking()
                .Where(token =>
                    token.token_hash == tokenHash &&
                    token.purpose == AccountTokenPurpose.PasswordReset)
                .Select(token => (Guid?)token.user_id)
                .SingleOrDefaultAsync(cancellationToken);

            if (userId is null)
            {
                return UnprocessableEntity(InvalidPasswordResetToken());
            }

            await using var transaction = await refreshSessionLock.AcquireAsync(
                userId.Value,
                cancellationToken);

            var now = DateTimeOffset.UtcNow;

            var accountToken = await db.AccountTokens
                .Include(token => token.user)
                .SingleOrDefaultAsync(
                    token =>
                        token.token_hash == tokenHash &&
                        token.purpose ==
                            AccountTokenPurpose.PasswordReset &&
                        token.consumed_at == null &&
                        token.expires_at > now,
                    cancellationToken);

            if (accountToken is null ||
                accountToken.user is null ||
                !accountToken.user.is_active ||
                !accountToken.user.email_confirmed)
            {
                return UnprocessableEntity(
                    InvalidPasswordResetToken());
            }

            var consumedRows = await db.AccountTokens
                .Where(token =>
                    token.id == accountToken.id &&
                    token.consumed_at == null &&
                    token.expires_at > now)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            token => token.consumed_at,
                            now),
                    cancellationToken);

            if (consumedRows != 1)
            {
                return UnprocessableEntity(
                    InvalidPasswordResetToken());
            }

            accountToken.consumed_at = now;

            accountToken.user.password_hash =
                passwordHasher.HashPassword(
                    accountToken.user,
                    request.NewPassword);

            accountToken.user.failed_login_count = 0;
            accountToken.user.lockout_end = null;

            await db.RefreshTokens
                .Where(token =>
                    token.user_id == accountToken.user.id &&
                    token.revoked_at == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            token => token.revoked_at,
                            now),
                    cancellationToken);

            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            Response.Cookies.Delete(
                AuthCookieOptions.RefreshTokenName,
                AuthCookieOptions.CreateDeleteOptions());

            return Ok(new ResetPasswordResponse(
                "Parola a fost resetată cu succes."));
        }

        [AllowAnonymous]
        [HttpPost("resend-confirmation")]
        public async Task<
            ActionResult<ResendConfirmationResponse>>
            ResendConfirmation(
                ResendConfirmationRequest request,
                CancellationToken cancellationToken)
        {
            var message =
                "Dacă adresa există și nu este confirmată, " +
                "vei primi un nou link de confirmare.";

            var email = request.Email
                .Trim()
                .ToLowerInvariant();

            var user = await db.Users
                .SingleOrDefaultAsync(
                    item => item.email == email,
                    cancellationToken);

            if (user is null ||
                !user.is_active ||
                user.email_confirmed)
            {
                return Accepted(
                    new ResendConfirmationResponse(
                        message,
                        null));
            }

            var now = DateTimeOffset.UtcNow;

            var previousTokens = await db.AccountTokens
                .Where(token =>
                    token.user_id == user.id &&
                    token.purpose ==
                        AccountTokenPurpose.EmailConfirmation &&
                    token.consumed_at == null)
                .ToListAsync(cancellationToken);

            foreach (var token in previousTokens)
            {
                token.consumed_at = now;
            }

            var generatedToken =
                accountTokenService.Create(
                    user.id,
                    AccountTokenPurpose.EmailConfirmation,
                    TimeSpan.FromHours(24));

            db.AccountTokens.Add(
                generatedToken.Entity);

            await db.SaveChangesAsync(
                cancellationToken);

            await accountEmailService.SendConfirmationAsync(
                user,
                generatedToken.RawToken,
                cancellationToken);

            return Accepted(
                new ResendConfirmationResponse(
                    message,
                    environment.IsDevelopment()
                        ? generatedToken.RawToken
                        : null));
        }

        private static ProblemDetails InvalidPasswordResetToken()
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Token de resetare invalid",
                Detail = "Tokenul este invalid, expirat sau a fost deja folosit."
            };
        }

        private Guid? GetCurrentUserId()
        {
            var value = User.FindFirstValue(
                JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(value, out var userId)
                ? userId
                : null;
        }

        private static ProblemDetails InvalidRefreshToken()
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Refresh token invalid",
                Detail = "Sesiunea nu mai este validă."
            };
        }

        private static ProblemDetails InvalidConfirmationToken()
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Token de confirmare invalid",
                Detail = "Tokenul este invalid, expirat sau a fost deja folosit."
            };
        }

        private async Task<TokenResponse> IssueTokensAsync(
            User user,
            CancellationToken cancellationToken)
        {
            var tokens = tokenService.Create(user);
            db.RefreshTokens.Add(tokens.RefreshTokenEntity);
            await db.SaveChangesAsync(cancellationToken);

            Response.Cookies.Append(
                AuthCookieOptions.RefreshTokenName,
                tokens.RefreshToken,
                AuthCookieOptions.Create(
                    tokens.RefreshTokenEntity.expires_at));

            return new TokenResponse(
                tokens.AccessToken,
                tokens.AccessTokenExpiresAt);
        }

        private async Task RegisterFailedLoginAsync(
            User user,
            CancellationToken cancellationToken)
        {
            user.failed_login_count++;

            if (user.failed_login_count >= 5)
            {
                user.lockout_end = DateTimeOffset.UtcNow.AddMinutes(15);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        private bool TryConsumeRecoveryCode(UserTotp userTotp, string code)
        {
            var hash = totpService.HashRecoveryCode(code);
            var hashes = userTotp.recovery_code_hashes.RootElement
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToList();

            var match = hashes.SingleOrDefault(item =>
                CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(item),
                    System.Text.Encoding.ASCII.GetBytes(hash)));

            if (match is null)
            {
                return false;
            }

            hashes.Remove(match);
            userTotp.recovery_code_hashes = JsonDocument.Parse(
                JsonSerializer.Serialize(hashes));

            return true;
        }

        private static ProblemDetails InvalidLogin()
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Autentificare eșuată",
                Detail = "Datele de autentificare sunt invalide."
            };
        }

        private ProblemDetails InvalidTotpCode()
        {
            return ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Cod TOTP invalid",
                "Codul este invalid, expirat sau a fost deja folosit.");
        }


        private async Task RevokeTokenFamilyAsync(
            Guid userId,
            Guid tokenId,
            DateTimeOffset revokedAt,
            CancellationToken cancellationToken)
        {
            var tokens = await db.RefreshTokens
                .Where(token => token.user_id == userId)
                .ToListAsync(cancellationToken);

            var tokensById = tokens.ToDictionary(
                token => token.id);

            var predecessorsByChildId = tokens
                .Where(token => token.replaced_by_id.HasValue)
                .GroupBy(token => token.replaced_by_id!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(token => token.id).ToArray());

            var familyIds = new HashSet<Guid>
                {
                    tokenId
                };

            var pending = new Queue<Guid>();
            pending.Enqueue(tokenId);

            while (pending.Count > 0)
            {
                var currentId = pending.Dequeue();

                if (tokensById.TryGetValue(
                        currentId,
                        out var currentToken) &&
                    currentToken.replaced_by_id is Guid childId &&
                    familyIds.Add(childId))
                {
                    pending.Enqueue(childId);
                }

                if (predecessorsByChildId.TryGetValue(
                        currentId,
                        out var predecessorIds))
                {
                    foreach (var predecessorId in predecessorIds)
                    {
                        if (familyIds.Add(predecessorId))
                        {
                            pending.Enqueue(predecessorId);
                        }
                    }
                }
            }

            foreach (var token in tokens)
            {
                if (familyIds.Contains(token.id))
                {
                    token.revoked_at ??= revokedAt;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
