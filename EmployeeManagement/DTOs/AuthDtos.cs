namespace EmployeeManagement.DTOs;

public class LoginRequest
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public class LoginRequestDto
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public class LoginResponse
{
    public string Token { get; set; } = null!;
    public string AccountId { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? EmployeeId { get; set; }
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    public DateTime ExpiresAt { get; set; }
}

public class LoginResponseDto
{
    public string AccountId { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string EmployeeId { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}

public class UserAccessDto
{
    public string AccountId { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string EmployeeId { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ManagedProjectIds { get; set; } = Array.Empty<string>();
    public bool CanViewAllCompanyData { get; set; }
    public bool CanManageRoles { get; set; }
    public bool CanViewAvailability { get; set; }
}
