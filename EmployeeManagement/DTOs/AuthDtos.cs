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
    public DateTime ExpiresAt { get; set; }
}

public class LoginResponseDto
{
    public string AccountId { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string EmployeeId { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
}
