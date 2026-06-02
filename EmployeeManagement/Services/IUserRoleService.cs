using EmployeeManagement.DTOs;

namespace EmployeeManagement.Services;

public interface IUserRoleService
{
    Task<string> GetRoleForEmployeeAsync(string employeeId, string username);
    Task<string> GetRoleForAccountAsync(string accountId, string username);
    IReadOnlyList<string> GetPermissions(string role);
    Task<UserAccessDto?> GetAccessForEmployeeAsync(string employeeId);
    Task<List<EmployeeRoleDto>> GetEmployeeRolesAsync();
}
