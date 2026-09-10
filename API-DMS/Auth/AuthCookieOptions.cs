namespace API_DMS.Auth
{
    public static class AuthCookieOptions
    {
        public const string RefreshTokenName =
            "__Host-dms-refresh";

        public static CookieOptions Create(
            DateTimeOffset expiresAt)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
                Expires = expiresAt,
                Path = "/"
            };
        }

        public static CookieOptions CreateDeleteOptions()
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
                Path = "/"
            };
        }
    }
}
