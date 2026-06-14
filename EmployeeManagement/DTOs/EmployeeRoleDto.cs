namespace EmployeeManagement.DTOs;

public class EmployeeRoleDto
{
    public string EmployeeId { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Role { get; set; } = null!;
    public IReadOnlyList<string> ManagedProjectIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ManagedProjectNames { get; set; } = Array.Empty<string>();
}

public class UpdateEmployeeRoleDto
{
    public string Role { get; set; } = null!;
    public List<string> ManagedProjectIds { get; set; } = new();
}

public class EmployeeRoleUpdateResult
{
    public EmployeeRoleDto? EmployeeRole { get; set; }
    public string? Error { get; set; }
    public bool Success => EmployeeRole != null && string.IsNullOrWhiteSpace(Error);
}
