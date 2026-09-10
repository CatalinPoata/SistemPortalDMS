using API_PORTAL.Entities.Base;
using System.Text.Json;

namespace API_PORTAL.Entities
{
    public enum SurveyQuestionType
    {
        SingleChoice,
        MultiChoice,
        Rating,
        FreeText
    }
    public class SurveyQuestion : BaseEntity
    {
        public Guid id { get; set; }

        public Guid survey_id { get; set; }
        public Survey survey { get; set; } = null!;

        public string key { get; set; } = null!;
        public string text { get; set; } = null!;

        public SurveyQuestionType type { get; set; }

        public JsonDocument? options { get; set; }

        public bool is_required { get; set; } = false;
        public int display_order { get; set; }
    }
}
