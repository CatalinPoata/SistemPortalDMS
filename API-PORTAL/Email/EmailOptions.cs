namespace API_PORTAL.Email
{
    public sealed class EmailOptions
    {
        public const string SectionName = "Email";

        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 1025;
        public bool UseSsl { get; set; }

        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = "Sistem Portal DMS";

        public string PublicAppBaseUrl { get; set; } = string.Empty;
    }
}
