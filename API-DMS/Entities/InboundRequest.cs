using System.Text.Json;
using API_DMS.Entities.Base;

namespace API_DMS.Entities
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
