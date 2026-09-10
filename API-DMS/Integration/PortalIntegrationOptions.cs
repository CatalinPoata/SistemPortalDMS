namespace API_DMS.Integration
{
    public sealed class PortalIntegrationOptions
    {
        public const string SectionName = "Integration:Portal";

        public string BaseUrl { get; set; } = string.Empty;

        public string SharedSecret { get; set; } = string.Empty;

        public string ActorEmail { get; set; } = "admin@example.com";
    }
}
