using System.Security.Claims;
using EmployeeManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Services;

public class AccessScopeService : IAccessScopeService
{
    private readonly AppDbContext _context;

    public AccessScopeService(AppDbContext context)
    {
        _context = context;
    }

    public string? GetCurrentEmployeeId(ClaimsPrincipal user) => user.FindFirst("employee_id")?.Value;

    public async Task<IReadOnlyList<string>> GetManagedProjectIdsAsync(ClaimsPrincipal user)
    {
        if (!user.IsInRole(RoleNames.Manager))
            return Array.Empty<string>();

        var employeeId = GetCurrentEmployeeId(user);
        if (string.IsNullOrWhiteSpace(employeeId))
            return Array.Empty<string>();

        return await _context.ProjectManagers
            .AsNoTracking()
            .Where(pm => pm.EmployeeId == employeeId)
            .Select(pm => pm.ProjectId)
            .Distinct()
            .ToListAsync();
    }

    public Task<bool> CanUseViewerIdAsync(ClaimsPrincipal user, string viewerEmployeeId)
    {
        return Task.FromResult(user.IsInRole(RoleNames.Admin) || GetCurrentEmployeeId(user) == viewerEmployeeId);
    }

    public async Task<bool> CanViewEmployeeAsync(ClaimsPrincipal user, string employeeId)
    {
        if (user.IsInRole(RoleNames.Admin) || GetCurrentEmployeeId(user) == employeeId)
            return true;
        if (!user.IsInRole(RoleNames.Manager))
            return false;

        var managedProjectIds = await GetManagedProjectIdsAsync(user);
        return await IsEmployeeInProjectsAsync(employeeId, managedProjectIds);
    }

    public async Task<bool> CanManageEmployeeAsync(ClaimsPrincipal user, string employeeId)
    {
        if (user.IsInRole(RoleNames.Admin))
            return true;
        if (!user.IsInRole(RoleNames.Manager))
            return false;

        var managedProjectIds = await GetManagedProjectIdsAsync(user);
        return await IsEmployeeInProjectsAsync(employeeId, managedProjectIds);
    }

    public async Task<bool> CanViewProjectAsync(ClaimsPrincipal user, string projectId)
    {
        if (user.IsInRole(RoleNames.Admin))
            return true;

        var employeeId = GetCurrentEmployeeId(user);
        if (string.IsNullOrWhiteSpace(employeeId))
            return false;

        if (user.IsInRole(RoleNames.Manager))
            return await _context.ProjectManagers.AsNoTracking()
                .AnyAsync(pm => pm.EmployeeId == employeeId && pm.ProjectId == projectId);

        return await _context.Allocations.AsNoTracking()
            .AnyAsync(a => a.EmployeeId == employeeId && a.ProjectId == projectId);
    }

    public async Task<bool> CanManageProjectAsync(ClaimsPrincipal user, string projectId)
    {
        if (user.IsInRole(RoleNames.Admin))
            return true;
        if (!user.IsInRole(RoleNames.Manager))
            return false;

        var employeeId = GetCurrentEmployeeId(user);
        return !string.IsNullOrWhiteSpace(employeeId) &&
            await _context.ProjectManagers.AsNoTracking()
                .AnyAsync(pm => pm.EmployeeId == employeeId && pm.ProjectId == projectId);
    }

    public async Task<bool> CanViewTaskAsync(ClaimsPrincipal user, string projectId, string taskId)
    {
        if (user.IsInRole(RoleNames.Admin) || await CanManageProjectAsync(user, projectId))
            return true;

        var employeeId = GetCurrentEmployeeId(user);
        return !string.IsNullOrWhiteSpace(employeeId) &&
            await _context.Allocations.AsNoTracking().AnyAsync(a =>
                a.EmployeeId == employeeId && a.ProjectId == projectId && a.TaskId == taskId);
    }

    private async Task<bool> IsEmployeeInProjectsAsync(string employeeId, IReadOnlyList<string> projectIds)
    {
        if (projectIds.Count == 0)
            return false;

        return await _context.Allocations.AsNoTracking()
            .AnyAsync(a => a.EmployeeId == employeeId && projectIds.Contains(a.ProjectId)) ||
            await _context.ProjectManagers.AsNoTracking()
                .AnyAsync(pm => pm.EmployeeId == employeeId && projectIds.Contains(pm.ProjectId));
    }
}
