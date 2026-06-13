using EmployeeManagement.DTOs;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccessController : ControllerBase
{
    private readonly IUserRoleService _userRoleService;

    public AccessController(IUserRoleService userRoleService)
    {
        _userRoleService = userRoleService;
    }

    [HttpGet("employee/{employeeId}")]
    public async Task<ActionResult<UserAccessDto>> GetForEmployee(string employeeId)
    {
        var currentEmployeeId = User.FindFirst("employee_id")?.Value;
        if (!User.IsInRole(RoleNames.Admin) && currentEmployeeId != employeeId)
            return Forbid();

        var access = await _userRoleService.GetAccessForEmployeeAsync(employeeId);
        if (access == null)
            return NotFound("Employee access context was not found.");

        return Ok(access);
    }
}
