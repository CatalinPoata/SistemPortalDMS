using API_PORTAL.Entities.Base;
using System.Text.Json;

namespace API_PORTAL.Entities
{
    public class InboundRequest : BaseEntity
    {
        public string endpoint { get; set; } = null!;

        public string idempotency_key { get; set; } = null!;

        public string request_hash { get; set; } = null!;

        public int response_status { get; set; }

        public JsonDocument response_body { get; set; } = null!;
    }
}
