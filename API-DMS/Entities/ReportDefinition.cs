using API_DMS.Entities.Base;
using System.Text.Json;

namespace API_DMS.Entities
{
    public class ReportDefinition : BaseEntity
    {
        public Guid id { get; set; }

        public string code { get; set; } = null!;

        public string name { get; set; } = null!;

        public string dataset_key { get; set; } = null!;

        public JsonDocument definition { get; set; } = null!;

        public int version { get; set; } = 1;

        public bool is_system { get; set; } = false;

        public Guid updated_by_user_id { get; set; }
        public User updated_by_user { get; set; } = null!;
    }
}
