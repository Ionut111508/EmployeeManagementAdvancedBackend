using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Data;
using EmployeeManagement.Entities;
using EmployeeManagement.DTOs;
using EmployeeManagement.Services;

namespace EmployeeManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AccountsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AccountDto>>> GetAll()
        {
            var accounts = await _context.Accounts.AsNoTracking().ToListAsync();
            var dtos = accounts.Select(a => new AccountDto
            {
                AccountId = a.AccountId,
                Username = a.Username,
                Role = RoleNames.Normalize(a.Role)
            }).ToList();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AccountDto>> GetById(string id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
                return NotFound("Account was not found.");

            return Ok(new AccountDto
            {
                AccountId = account.AccountId,
                Username = account.Username,
                Role = RoleNames.Normalize(account.Role)
            });
        }

        [HttpPost]
        public async Task<ActionResult<AccountDto>> Create(AccountCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.AccountId) || string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("AccountId, Username and Password are required.");

            if (!RoleNames.IsValid(dto.Role))
                return BadRequest("Role must be Admin, Manager or Employee.");

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
                Password = dto.Password,
                Role = RoleNames.Normalize(dto.Role)
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

            if (!RoleNames.IsValid(dto.Role))
                return BadRequest("Role must be Admin, Manager or Employee.");

            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
                return NotFound("Account was not found.");

            var usernameExists = await _context.Accounts.AnyAsync(a => a.Username == dto.Username && a.AccountId != id);
            if (usernameExists)
                return BadRequest("Username is already used by another account.");

            account.Username = dto.Username;
            account.Role = RoleNames.Normalize(dto.Role);
            if (!string.IsNullOrWhiteSpace(dto.Password))
                account.Password = dto.Password;

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
