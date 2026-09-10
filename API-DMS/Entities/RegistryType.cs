using API_DMS.Entities.Base;

namespace API_DMS.Entities
{
    public enum RegistryDirection
    {
        In,
        Out,
        Both
    }

    public class RegistryType : BaseEntity
    {
        public Guid id { get; set; }
        public string code { get; set; } = null!;
        public string name { get; set; } = null!;
        public RegistryDirection direction { get; set; }
        public long start_number { get; set; } = 1;
        public int default_deadline_days { get; set; } = 30;
        public bool is_closed { get; set; } = false;
    }
}
