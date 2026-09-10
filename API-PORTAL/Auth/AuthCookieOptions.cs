namespace API_PORTAL.Auth
{
    public static class AuthCookieOptions
    {
        public const string RefreshTokenName =
            "__Host-portal-refresh";

        public static CookieOptions Create(DateTimeOffset expiresAt)
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
