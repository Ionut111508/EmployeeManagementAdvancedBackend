using EmployeeManagement.DTOs;

namespace EmployeeManagement.Services;

public interface IUserRoleService
{
    Task<string> GetRoleForEmployeeAsync(string employeeId, string username);
    Task<string> GetRoleForAccountAsync(string accountId, string username);
    Task<List<EmployeeRoleDto>> GetEmployeeRolesAsync();
}
