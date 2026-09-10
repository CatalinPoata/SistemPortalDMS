namespace API_DMS.DTO.Departments
{
    public sealed class UpdateDepartmentRequest
    {
        public string? Code { get; set; }

        public string? Name { get; set; }

        public Guid? ManagerUserId { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
