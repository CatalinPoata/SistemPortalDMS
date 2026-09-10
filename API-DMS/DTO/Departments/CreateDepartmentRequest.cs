namespace API_DMS.DTO.Departments
{
    public sealed class CreateDepartmentRequest
    {
        public string? Code { get; set; }

        public string? Name { get; set; }

        public Guid? ManagerUserId { get; set; }
    }
}
