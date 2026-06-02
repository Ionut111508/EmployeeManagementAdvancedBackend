using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IUserRoleService _userRoleService;

    public AuthController(AppDbContext context, IUserRoleService userRoleService)
    {
        _context = context;
        _userRoleService = userRoleService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Username and password are required.");

        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Username == request.Username);

        if (account == null || account.Password != request.Password)
            return Unauthorized("Invalid username or password.");

        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.AccountId == account.AccountId);

        if (employee == null)
            return BadRequest("No employee is linked to this account.");

        var role = await _userRoleService.GetRoleForEmployeeAsync(employee.EmployeeId, account.Username);

        return Ok(new LoginResponseDto
        {
            AccountId = account.AccountId,
            Username = account.Username,
            EmployeeId = employee.EmployeeId,
            FullName = $"{employee.FirstName} {employee.LastName}",
            Role = role,
            Permissions = _userRoleService.GetPermissions(role)
        });
    }
}
