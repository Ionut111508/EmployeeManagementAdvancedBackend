using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IUserRoleService _userRoleService;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;

    public AuthController(AppDbContext context, IUserRoleService userRoleService, IPasswordService passwordService, ITokenService tokenService)
    {
        _context = context;
        _userRoleService = userRoleService;
        _passwordService = passwordService;
        _tokenService = tokenService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Username and password are required.");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Username == request.Username);

        if (account == null || !_passwordService.VerifyPassword(account.Password, request.Password, out var needsRehash))
            return Unauthorized("Invalid username or password.");

        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.AccountId == account.AccountId);

        if (employee == null)
            return BadRequest("No employee is linked to this account.");

        var role = await _userRoleService.GetRoleForEmployeeAsync(employee.EmployeeId, account.Username);
        if (needsRehash)
        {
            account.Password = _passwordService.HashPassword(request.Password);
            await _context.SaveChangesAsync();
        }
        var token = _tokenService.CreateToken(account, employee, role);

        return Ok(new LoginResponseDto
        {
            Token = token.Token,
            AccountId = account.AccountId,
            Username = account.Username,
            EmployeeId = employee.EmployeeId,
            FullName = $"{employee.FirstName} {employee.LastName}",
            Role = role,
            Permissions = _userRoleService.GetPermissions(role),
            ExpiresAt = token.ExpiresAt
        });
    }
}
