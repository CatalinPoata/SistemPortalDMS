namespace API_DMS.DTO.RegistryEntries
{
    public sealed class CreateRegistryTaskRequest
    {
        public Guid? AssigneeUserId { get; set; }

        public Guid? DepartmentId { get; set; }

        public string? Title { get; set; }

        public string? Instructions { get; set; }

        public DateOnly? DueDate { get; set; }
    }
}
