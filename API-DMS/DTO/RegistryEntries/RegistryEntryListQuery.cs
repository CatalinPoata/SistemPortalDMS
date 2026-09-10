using API_DMS.Entities;

namespace API_DMS.DTO.RegistryEntries
{
    public sealed class RegistryEntryListQuery
    {
        public Guid? RegistryTypeId { get; set; }
        public int? Year { get; set; }
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }
        public long? MinNumber { get; set; }
        public long? MaxNumber { get; set; }
        public EntryStatus? Status { get; set; }
        public Guid? DepartmentId { get; set; }
        public string? Search { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string? Sort { get; set; }
    }
}
