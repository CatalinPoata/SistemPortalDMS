using API_DMS.Entities.Base;

namespace API_DMS.Entities
{
    public class Department : BaseEntity
    {
        public Guid id { get; set; }
        public string code { get; set; } = null!;
        public string name { get; set; } = null!;

        public Guid? manager_user_id { get; set; }
        public User? manager_user { get; set; }

        public bool is_active { get; set; } = true;
    }
}
