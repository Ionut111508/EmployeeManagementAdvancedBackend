using EmployeeManagement.DTOs;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.Admin)]
public class RolesController : ControllerBase
{
    private readonly IUserRoleService _userRoleService;

    public RolesController(IUserRoleService userRoleService)
    {
        _userRoleService = userRoleService;
    }

    [HttpGet("employees")]
    public async Task<ActionResult<IEnumerable<EmployeeRoleDto>>> GetEmployeeRoles()
    {
        return Ok(await _userRoleService.GetEmployeeRolesAsync());
    }
}
