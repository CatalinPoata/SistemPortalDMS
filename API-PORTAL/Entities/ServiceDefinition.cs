using API_PORTAL.Entities.Base;
using System.Text.Json;

namespace API_PORTAL.Entities
{
    public class ServiceDefinition : BaseEntity
    {
        public Guid id { get; set; }

        public string code { get; set; } = null!;
        public string title { get; set; } = null!;
        public string? short_description { get; set; }
        public string? description { get; set; }
        public string registry_type_code { get; set; } = null!;
        public JsonDocument form_schema { get; set; } = null!;
        public int schema_version { get; set; } = 1;
        public bool requires_attachment { get; set; } = false;
        public int max_attachments { get; set; } = 3;
        public bool is_published { get; set; } = false;
        public int display_order { get; set; } = 0;
    }
}
