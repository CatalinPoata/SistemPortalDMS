using API_DMS.Entities.Base;

namespace API_DMS.Entities
{
    public class RegistryNumberCounter : BaseEntity
    {
        public Guid registry_type_id { get; set; }
        public RegistryType registry_type { get; set; } = null!;

        public int year { get; set; }
        public long last_number { get; set; }
    }
}
