using System.Security.Claims;

namespace EmployeeManagement.Services;

public interface IAccessScopeService
{
    string? GetCurrentEmployeeId(ClaimsPrincipal user);
    Task<IReadOnlyList<string>> GetManagedProjectIdsAsync(ClaimsPrincipal user);
    Task<bool> CanUseViewerIdAsync(ClaimsPrincipal user, string viewerEmployeeId);
    Task<bool> CanViewEmployeeAsync(ClaimsPrincipal user, string employeeId);
    Task<bool> CanManageEmployeeAsync(ClaimsPrincipal user, string employeeId);
    Task<bool> CanViewProjectAsync(ClaimsPrincipal user, string projectId);
    Task<bool> CanManageProjectAsync(ClaimsPrincipal user, string projectId);
    Task<bool> CanViewTaskAsync(ClaimsPrincipal user, string projectId, string taskId);
}
