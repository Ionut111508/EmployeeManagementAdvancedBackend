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
        var accountId = await _context.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeId == employeeId)
            .Select(e => e.AccountId)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(accountId)
            ? RoleNames.Employee
            : RoleNames.Normalize(await ReadAccountRoleAsync(accountId));
    }

    public async Task<string> GetRoleForAccountAsync(string accountId, string username)
    {
        return RoleNames.Normalize(await ReadAccountRoleAsync(accountId));
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

        var assignmentRows = await _context.ProjectManagers
            .AsNoTracking()
            .Include(pm => pm.Project)
            .ToListAsync();
        var managerAssignments = assignmentRows
            .GroupBy(pm => pm.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(pm => pm.Project!.ProjectName).ToList());

        return employees.Select(employee =>
        {
            managerAssignments.TryGetValue(employee.EmployeeId, out var assignments);
            return new EmployeeRoleDto
            {
                EmployeeId = employee.EmployeeId,
                FullName = $"{employee.FirstName} {employee.LastName}",
                Username = employee.Account?.Username ?? string.Empty,
                Role = RoleNames.Normalize(employee.Account?.Role),
                ManagedProjectIds = assignments?.Select(pm => pm.ProjectId).ToList() ?? new List<string>(),
                ManagedProjectNames = assignments?.Select(pm => pm.Project?.ProjectName ?? pm.ProjectId).ToList() ?? new List<string>()
            };
        }).ToList();
    }

    public async Task<EmployeeRoleUpdateResult> UpdateEmployeeRoleAsync(string employeeId, UpdateEmployeeRoleDto request)
    {
        if (!RoleNames.IsValid(request.Role))
            return new EmployeeRoleUpdateResult { Error = "Role must be Admin, Manager or Employee." };

        var employee = await _context.Employees
            .Include(e => e.Account)
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee?.Account == null)
            return new EmployeeRoleUpdateResult { Error = "Employee does not have a linked account." };

        var role = RoleNames.Normalize(request.Role);
        var projectIds = (role == RoleNames.Manager ? request.ManagedProjectIds : new List<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (role == RoleNames.Manager && projectIds.Count == 0)
            return new EmployeeRoleUpdateResult { Error = "A manager must be assigned to at least one project." };

        var projects = await _context.Projects
            .Where(p => projectIds.Contains(p.ProjectId))
            .OrderBy(p => p.ProjectName)
            .ToListAsync();
        if (projects.Count != projectIds.Count)
            return new EmployeeRoleUpdateResult { Error = "One or more selected projects do not exist." };

        if (RoleNames.Normalize(employee.Account.Role) == RoleNames.Admin && role != RoleNames.Admin)
        {
            var otherAdmins = await _context.Accounts.CountAsync(a => a.AccountId != employee.AccountId && a.Role == RoleNames.Admin);
            if (otherAdmins == 0)
                return new EmployeeRoleUpdateResult { Error = "The last administrator cannot be demoted." };
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        employee.Account.Role = role;

        var existingAssignments = await _context.ProjectManagers
            .Where(pm => pm.EmployeeId == employeeId)
            .ToListAsync();
        _context.ProjectManagers.RemoveRange(existingAssignments);

        if (role == RoleNames.Manager)
        {
            _context.ProjectManagers.AddRange(projects.Select(project => new Entities.ProjectManager
            {
                EmployeeId = employeeId,
                ProjectId = project.ProjectId,
                StartDate = DateTime.Today
            }));
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new EmployeeRoleUpdateResult
        {
            EmployeeRole = new EmployeeRoleDto
            {
                EmployeeId = employee.EmployeeId,
                FullName = $"{employee.FirstName} {employee.LastName}",
                Username = employee.Account.Username,
                Role = role,
                ManagedProjectIds = role == RoleNames.Manager ? projects.Select(p => p.ProjectId).ToList() : Array.Empty<string>(),
                ManagedProjectNames = role == RoleNames.Manager ? projects.Select(p => p.ProjectName).ToList() : Array.Empty<string>()
            }
        };
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
}
