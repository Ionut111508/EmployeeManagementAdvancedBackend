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
    public class EmployeesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IUserRoleService _userRoleService;

        public EmployeesController(AppDbContext context, IUserRoleService userRoleService)
        {
            _context = context;
            _userRoleService = userRoleService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetAll()
        {
            var employees = await _context.Employees
                .Include(e => e.Account)
                .Include(e => e.WorkNorm)
                .ToListAsync();

            var dtos = employees.Select(e => new EmployeeDto
            {
                EmployeeId = e.EmployeeId,
                LastName = e.LastName,
                FirstName = e.FirstName,
                Email = e.Email,
                PhoneNumber = e.PhoneNumber,
                AccountId = e.AccountId,
                WorkNormId = e.WorkNormId,
                Account = e.Account != null ? new AccountDto { AccountId = e.Account.AccountId, Username = e.Account.Username, Role = RoleNames.Normalize(e.Account.Role) } : null,
                WorkNorm = e.WorkNorm != null ? new WorkNormDto { WorkNormId = e.WorkNorm.WorkNormId, WorkNormName = e.WorkNorm.WorkNormName, WorkHours = e.WorkNorm.WorkHours } : null
            }).ToList();

            return Ok(dtos);
        }

        [HttpGet("visible-to/{viewerEmployeeId}")]
        public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetVisibleTo(string viewerEmployeeId)
        {
            var access = await _userRoleService.GetAccessForEmployeeAsync(viewerEmployeeId);
            if (access == null)
                return NotFound("Viewer employee was not found.");

            IQueryable<Employee> query = _context.Employees
                .AsNoTracking()
                .Include(e => e.Account)
                .Include(e => e.WorkNorm);

            if (access.Role == RoleNames.Employee)
            {
                query = query.Where(e => e.EmployeeId == viewerEmployeeId);
            }
            else if (access.Role == RoleNames.Manager)
            {
                var managedProjectIds = access.ManagedProjectIds;
                query = query.Where(e =>
                    e.EmployeeId == viewerEmployeeId ||
                    _context.ProjectManagers.Any(pm => pm.EmployeeId == e.EmployeeId && managedProjectIds.Contains(pm.ProjectId)) ||
                    _context.Allocations.Any(a => a.EmployeeId == e.EmployeeId && managedProjectIds.Contains(a.ProjectId)));
            }

            var employees = await query
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .ToListAsync();

            return Ok(employees.Select(ToDto).ToList());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<EmployeeDto>> GetById(string id)
        {
            var employee = await _context.Employees
                .Include(e => e.Account)
                .Include(e => e.WorkNorm)
                .FirstOrDefaultAsync(e => e.EmployeeId == id);

            if (employee == null)
                return NotFound("Employee was not found.");

            var dto = new EmployeeDto
            {
                EmployeeId = employee.EmployeeId,
                LastName = employee.LastName,
                FirstName = employee.FirstName,
                Email = employee.Email,
                PhoneNumber = employee.PhoneNumber,
                AccountId = employee.AccountId,
                WorkNormId = employee.WorkNormId,
                Account = employee.Account != null ? new AccountDto { AccountId = employee.Account.AccountId, Username = employee.Account.Username, Role = RoleNames.Normalize(employee.Account.Role) } : null,
                WorkNorm = employee.WorkNorm != null ? new WorkNormDto { WorkNormId = employee.WorkNorm.WorkNormId, WorkNormName = employee.WorkNorm.WorkNormName, WorkHours = employee.WorkNorm.WorkHours } : null
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<EmployeeDto>> Create(EmployeeCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.EmployeeId) ||
                string.IsNullOrWhiteSpace(dto.LastName) ||
                string.IsNullOrWhiteSpace(dto.FirstName) ||
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.PhoneNumber) ||
                string.IsNullOrWhiteSpace(dto.AccountId) ||
                string.IsNullOrWhiteSpace(dto.WorkNormId))
                return BadRequest("EmployeeId, LastName, FirstName, Email, PhoneNumber, AccountId and WorkNormId are required.");

            var employeeExists = await _context.Employees.AnyAsync(e => e.EmployeeId == dto.EmployeeId);
            if (employeeExists)
                return BadRequest("Employee with this ID already exists.");

            var accountExists = await _context.Accounts.AnyAsync(a => a.AccountId == dto.AccountId);
            if (!accountExists)
                return BadRequest("Selected account does not exist.");

            var accountUsed = await _context.Employees.AnyAsync(e => e.AccountId == dto.AccountId);
            if (accountUsed)
                return BadRequest("Selected account is already assigned to another employee.");

            var workNormExists = await _context.WorkNorms.AnyAsync(w => w.WorkNormId == dto.WorkNormId);
            if (!workNormExists)
                return BadRequest("Selected work norm does not exist.");

            var employee = new Employee
            {
                EmployeeId = dto.EmployeeId,
                LastName = dto.LastName,
                FirstName = dto.FirstName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                AccountId = dto.AccountId,
                WorkNormId = dto.WorkNormId
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            await _context.Entry(employee).Reference(e => e.Account).LoadAsync();
            await _context.Entry(employee).Reference(e => e.WorkNorm).LoadAsync();

            var resultDto = new EmployeeDto
            {
                EmployeeId = employee.EmployeeId,
                LastName = employee.LastName,
                FirstName = employee.FirstName,
                Email = employee.Email,
                PhoneNumber = employee.PhoneNumber,
                AccountId = employee.AccountId,
                WorkNormId = employee.WorkNormId,
                Account = employee.Account != null ? new AccountDto { AccountId = employee.Account.AccountId, Username = employee.Account.Username, Role = RoleNames.Normalize(employee.Account.Role) } : null,
                WorkNorm = employee.WorkNorm != null ? new WorkNormDto { WorkNormId = employee.WorkNorm.WorkNormId, WorkNormName = employee.WorkNorm.WorkNormName, WorkHours = employee.WorkNorm.WorkHours } : null
            };

            return CreatedAtAction(nameof(GetById), new { id = employee.EmployeeId }, resultDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, EmployeeUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.LastName) ||
                string.IsNullOrWhiteSpace(dto.FirstName) ||
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.PhoneNumber) ||
                string.IsNullOrWhiteSpace(dto.AccountId) ||
                string.IsNullOrWhiteSpace(dto.WorkNormId))
                return BadRequest("LastName, FirstName, Email, PhoneNumber, AccountId and WorkNormId are required.");

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
                return NotFound("Employee was not found.");

            var accountExists = await _context.Accounts.AnyAsync(a => a.AccountId == dto.AccountId);
            if (!accountExists)
                return BadRequest("Selected account does not exist.");

            var accountUsed = await _context.Employees.AnyAsync(e => e.AccountId == dto.AccountId && e.EmployeeId != id);
            if (accountUsed)
                return BadRequest("Selected account is already assigned to another employee.");

            var workNormExists = await _context.WorkNorms.AnyAsync(w => w.WorkNormId == dto.WorkNormId);
            if (!workNormExists)
                return BadRequest("Selected work norm does not exist.");

            employee.LastName = dto.LastName;
            employee.FirstName = dto.FirstName;
            employee.Email = dto.Email;
            employee.PhoneNumber = dto.PhoneNumber;
            employee.AccountId = dto.AccountId;
            employee.WorkNormId = dto.WorkNormId;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
                return NotFound("Employee was not found.");

            var hasAllocations = await _context.Allocations.AnyAsync(a => a.EmployeeId == id);
            if (hasAllocations)
                return BadRequest("Employee cannot be deleted because it has allocations.");

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static EmployeeDto ToDto(Employee e) => new()
        {
            EmployeeId = e.EmployeeId,
            LastName = e.LastName,
            FirstName = e.FirstName,
            Email = e.Email,
            PhoneNumber = e.PhoneNumber,
            AccountId = e.AccountId,
            WorkNormId = e.WorkNormId,
            Account = e.Account != null ? new AccountDto
            {
                AccountId = e.Account.AccountId,
                Username = e.Account.Username,
                Role = RoleNames.Normalize(e.Account.Role)
            } : null,
            WorkNorm = e.WorkNorm != null ? new WorkNormDto
            {
                WorkNormId = e.WorkNorm.WorkNormId,
                WorkNormName = e.WorkNorm.WorkNormName,
                WorkHours = e.WorkNorm.WorkHours
            } : null
        };
    }
}
