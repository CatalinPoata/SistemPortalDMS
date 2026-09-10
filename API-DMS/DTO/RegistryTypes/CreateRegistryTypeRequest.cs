using API_DMS.Entities;
using System.ComponentModel.DataAnnotations;

namespace API_DMS.DTO.RegistryTypes
{
    public sealed class CreateRegistryTypeRequest
    {
        [Required]
        [StringLength(30)]
        public string Code { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = null!;

        [EnumDataType(typeof(RegistryDirection))]
        public RegistryDirection Direction { get; set; }

        [Range(1, long.MaxValue)]
        public long StartNumber { get; set; } = 1;

        [Range(0, int.MaxValue)]
        public int DefaultDeadlineDays { get; set; } = 30;
    }
}
