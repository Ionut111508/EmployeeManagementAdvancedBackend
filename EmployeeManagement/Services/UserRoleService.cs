using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Services;

public class UserRoleService : IUserRoleService
{
    private readonly AppDbContext _context;

    public UserRoleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GetRoleForEmployeeAsync(string employeeId, string username)
    {
        if (IsAdminUsername(username))
            return RoleNames.Admin;

        var accountId = await _context.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeId == employeeId)
            .Select(e => e.AccountId)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(accountId))
        {
            var accountRole = await ReadAccountRoleAsync(accountId);
            if (accountRole == RoleNames.Admin || accountRole == RoleNames.Manager)
                return accountRole;
        }

        var isManager = await _context.ProjectManagers
            .AsNoTracking()
            .AnyAsync(pm => pm.EmployeeId == employeeId);

        return isManager ? RoleNames.Manager : RoleNames.Employee;
    }

    public async Task<string> GetRoleForAccountAsync(string accountId, string username)
    {
        if (IsAdminUsername(username))
            return RoleNames.Admin;

        var accountRole = await ReadAccountRoleAsync(accountId);
        if (accountRole == RoleNames.Admin || accountRole == RoleNames.Manager)
            return accountRole;

        var employeeId = await _context.Employees
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .Select(e => e.EmployeeId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(employeeId))
            return RoleNames.Employee;

        var isManager = await _context.ProjectManagers
            .AsNoTracking()
            .AnyAsync(pm => pm.EmployeeId == employeeId);

        return isManager ? RoleNames.Manager : RoleNames.Employee;
    }

    public IReadOnlyList<string> GetPermissions(string role) => RoleNames.GetPermissions(role);

    public async Task<UserAccessDto?> GetAccessForEmployeeAsync(string employeeId)
    {
        var employee = await _context.Employees
            .AsNoTracking()
            .Include(e => e.Account)
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

        if (employee?.Account == null)
            return null;

        var role = await GetRoleForEmployeeAsync(employee.EmployeeId, employee.Account.Username);
        var managedProjectIds = await _context.ProjectManagers
            .AsNoTracking()
            .Where(pm => pm.EmployeeId == employee.EmployeeId)
            .Select(pm => pm.ProjectId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync();

        var permissions = GetPermissions(role);
        return new UserAccessDto
        {
            AccountId = employee.AccountId,
            Username = employee.Account.Username,
            EmployeeId = employee.EmployeeId,
            FullName = $"{employee.FirstName} {employee.LastName}",
            Role = role,
            Permissions = permissions,
            ManagedProjectIds = managedProjectIds,
            CanViewAllCompanyData = role == RoleNames.Admin,
            CanManageRoles = role == RoleNames.Admin,
            CanViewAvailability = role == RoleNames.Admin || role == RoleNames.Manager
        };
    }

    public async Task<List<EmployeeRoleDto>> GetEmployeeRolesAsync()
    {
        var employees = await _context.Employees
            .AsNoTracking()
            .Include(e => e.Account)
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .ToListAsync();

        var result = new List<EmployeeRoleDto>();
        foreach (var employee in employees)
        {
            var username = employee.Account?.Username ?? string.Empty;
            result.Add(new EmployeeRoleDto
            {
                EmployeeId = employee.EmployeeId,
                FullName = $"{employee.FirstName} {employee.LastName}",
                Username = username,
                Role = await GetRoleForEmployeeAsync(employee.EmployeeId, username)
            });
        }

        return result;
    }

    private async Task<string?> ReadAccountRoleAsync(string accountId)
    {
        try
        {
            var role = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.AccountId == accountId)
                .Select(a => a.Role)
                .FirstOrDefaultAsync();
            return string.IsNullOrWhiteSpace(role) ? null : RoleNames.Normalize(role);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsAdminUsername(string username) =>
        string.Equals(username, RoleNames.Admin, StringComparison.OrdinalIgnoreCase);
}
