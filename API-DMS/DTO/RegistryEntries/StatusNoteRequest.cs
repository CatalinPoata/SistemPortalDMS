using System.ComponentModel.DataAnnotations;

namespace API_DMS.DTO.RegistryEntries
{
    public sealed class StatusNoteRequest
    {
        [StringLength(1000)]
        public string? Note { get; set; }
    }
}
