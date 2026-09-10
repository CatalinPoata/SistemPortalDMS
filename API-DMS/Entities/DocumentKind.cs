using API_DMS.Entities.Base;

namespace API_DMS.Entities
{

    public class DocumentKind : BaseEntity
    {
        public Guid id { get; set; }
        public string code { get; set; } = null!;
        public string name { get; set; } = null!;
        public bool is_active { get; set; } = true;
    }
}
