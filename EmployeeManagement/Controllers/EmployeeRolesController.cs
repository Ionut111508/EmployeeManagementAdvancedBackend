using EmployeeManagement.DTOs;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.Admin)]
public class EmployeeRolesController : ControllerBase
{
    private readonly IUserRoleService _userRoleService;

    public EmployeeRolesController(IUserRoleService userRoleService)
    {
        _userRoleService = userRoleService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmployeeRoleDto>>> GetAll()
    {
        return Ok(await _userRoleService.GetEmployeeRolesAsync());
    }

    [HttpPut("{employeeId}")]
    public async Task<ActionResult<EmployeeRoleDto>> Update(string employeeId, UpdateEmployeeRoleDto request)
    {
        var result = await _userRoleService.UpdateEmployeeRoleAsync(employeeId, request);
        return result.Success ? Ok(result.EmployeeRole) : BadRequest(result.Error);
    }
}
