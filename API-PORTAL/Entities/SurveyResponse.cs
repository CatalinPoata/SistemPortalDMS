using API_PORTAL.Entities.Base;
using System.Text.Json;

namespace API_PORTAL.Entities
{
    public class SurveyResponse : BaseEntity
    {
        public Guid id { get; set; }

        public Guid survey_id { get; set; }
        public Survey survey { get; set; } = null!;

        public Guid? user_id { get; set; }
        public User? user { get; set; }

        public JsonDocument answers { get; set; } = null!;

        public DateTimeOffset submitted_at { get; set; }
    }
}
