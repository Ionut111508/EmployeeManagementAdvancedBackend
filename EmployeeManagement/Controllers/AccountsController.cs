using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Data;
using EmployeeManagement.Entities;
using EmployeeManagement.DTOs;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Authorization;

namespace EmployeeManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = RoleNames.Admin)]
    public class AccountsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPasswordService _passwordService;

        public AccountsController(AppDbContext context, IPasswordService passwordService)
        {
            _context = context;
            _passwordService = passwordService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AccountDto>>> GetAll()
        {
            var accounts = await _context.Accounts.AsNoTracking().OrderBy(a => a.Username).ToListAsync();
            var employees = await _context.Employees.AsNoTracking().ToDictionaryAsync(e => e.AccountId);
            var dtos = accounts.Select(a =>
            {
                employees.TryGetValue(a.AccountId, out var employee);
                return new AccountDto
                {
                    AccountId = a.AccountId,
                    Username = a.Username,
                    Role = RoleNames.Normalize(a.Role),
                    EmployeeId = employee?.EmployeeId,
                    EmployeeName = employee == null ? null : employee.FirstName + " " + employee.LastName
                };
            }).ToList();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AccountDto>> GetById(string id)
        {
            var account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountId == id);
            if (account == null)
                return NotFound("Account was not found.");

            var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.AccountId == id);
            return Ok(new AccountDto
            {
                AccountId = account.AccountId,
                Username = account.Username,
                Role = RoleNames.Normalize(account.Role),
                EmployeeId = employee?.EmployeeId,
                EmployeeName = employee == null ? null : employee.FirstName + " " + employee.LastName
            });
        }

        [HttpPost]
        public async Task<ActionResult<AccountDto>> Create(AccountCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.AccountId) || string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("AccountId, Username and Password are required.");

            if (dto.Password.Length < 8)
                return BadRequest("Password must contain at least 8 characters.");

            var accountIdExists = await _context.Accounts.AnyAsync(a => a.AccountId == dto.AccountId);
            if (accountIdExists)
                return BadRequest("Account with this ID already exists.");

            var usernameExists = await _context.Accounts.AnyAsync(a => a.Username == dto.Username);
            if (usernameExists)
                return BadRequest("Username is already used.");

            var account = new Account
            {
                AccountId = dto.AccountId,
                Username = dto.Username,
                Password = _passwordService.HashPassword(dto.Password),
                Role = RoleNames.Employee
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            var resultDto = new AccountDto
            {
                AccountId = account.AccountId,
                Username = account.Username,
                Role = account.Role
            };
            return CreatedAtAction(nameof(GetById), new { id = account.AccountId }, resultDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, AccountUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username))
                return BadRequest("Username is required.");

            if (!string.IsNullOrWhiteSpace(dto.Password) && dto.Password.Length < 8)
                return BadRequest("Password must contain at least 8 characters.");

            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
                return NotFound("Account was not found.");

            var usernameExists = await _context.Accounts.AnyAsync(a => a.Username == dto.Username && a.AccountId != id);
            if (usernameExists)
                return BadRequest("Username is already used by another account.");

            if (RoleNames.Normalize(dto.Role) != RoleNames.Normalize(account.Role))
                return BadRequest("Use employee role management to change a linked account role.");

            account.Username = dto.Username;
            if (!string.IsNullOrWhiteSpace(dto.Password))
                account.Password = _passwordService.HashPassword(dto.Password);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
                return NotFound("Account was not found.");

            var isUsedByEmployee = await _context.Employees.AnyAsync(e => e.AccountId == id);
            if (isUsedByEmployee)
                return BadRequest("Account cannot be deleted because it is assigned to an employee.");

            _context.Accounts.Remove(account);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
