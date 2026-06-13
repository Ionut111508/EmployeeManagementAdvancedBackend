namespace EmployeeManagement.DTOs
{
    public class AccountDto
    {
        public string AccountId { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Role { get; set; } = "Employee";
        public string? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
    }

    public class AccountCreateDto
    {
        public string AccountId { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Role { get; set; } = "Employee";
    }

    public class AccountUpdateDto
    {
        public string Username { get; set; } = null!;
        public string? Password { get; set; }
        public string Role { get; set; } = "Employee";
    }
}
