namespace API_PORTAL.Integration
{
    public sealed class DmsIntegrationOptions
    {
        public const string SectionName = "Integration:Dms";

        public string BaseUrl { get; set; } = string.Empty;

        public string SharedSecret { get; set; } = string.Empty;
    }
}
