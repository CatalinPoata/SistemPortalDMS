using API_DMS.Entities;
using System.ComponentModel.DataAnnotations;

namespace API_DMS.DTO.RegistryEntries
{
    public sealed class UploadRegistryDocumentRequest
    {
        [Required]
        public IFormFile? File { get; set; }

        [Required]
        public Guid DocumentKindId { get; set; }

        public DocumentDirection Direction { get; set; }

        public Guid? ExternalFileId { get; set; }

        public DateOnly? DocumentDate { get; set; }

        [StringLength(200)]
        public string? Issuer { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }
    }
}
