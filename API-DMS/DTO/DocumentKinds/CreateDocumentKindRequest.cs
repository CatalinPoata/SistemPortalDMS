using System.ComponentModel.DataAnnotations;

namespace API_DMS.DTO.DocumentKinds
{
    public sealed class CreateDocumentKindRequest
    {
        [Required]
        [StringLength(30)]
        public string Code { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = null!;
    }
}
