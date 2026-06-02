namespace EmployeeManagement.Entities
{
    public class Account
    {
        public string AccountId { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Role { get; set; } = "Employee";
    }
}
