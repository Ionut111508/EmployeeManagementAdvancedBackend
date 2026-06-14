using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Authorization;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeeDepartmentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAccessScopeService _accessScope;

    public EmployeeDepartmentsController(AppDbContext context, IAccessScopeService accessScope)
    {
        _context = context;
        _accessScope = accessScope;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        IQueryable<EmployeeDepartment> query = _context.EmployeeDepartments.AsNoTracking();
        if (User.IsInRole(RoleNames.Employee))
        {
            var employeeId = _accessScope.GetCurrentEmployeeId(User);
            query = query.Where(x => x.EmployeeId == employeeId);
        }
        else if (User.IsInRole(RoleNames.Manager))
        {
            var projectIds = await _accessScope.GetManagedProjectIdsAsync(User);
            query = query.Where(x => _context.Allocations.Any(a => a.EmployeeId == x.EmployeeId && projectIds.Contains(a.ProjectId)) ||
                _context.ProjectManagers.Any(pm => pm.EmployeeId == x.EmployeeId && projectIds.Contains(pm.ProjectId)));
        }

        var result = await query
            .Select(x => new EmployeeDepartmentResponse
            {
                EmployeeId = x.EmployeeId,
                DepartmentId = x.DepartmentId,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Employee = x.Employee != null ? new EmployeeBasicDto
                {
                    EmployeeId = x.Employee.EmployeeId,
                    FirstName = x.Employee.FirstName,
                    LastName = x.Employee.LastName,
                    Email = x.Employee.Email,
                    PhoneNumber = x.Employee.PhoneNumber,
                    AccountId = x.Employee.AccountId,
                    WorkNormId = x.Employee.WorkNormId
                } : null,
                Department = x.Department != null ? new DepartmentBasicDto
                {
                    DepartmentId = x.Department.DepartmentId,
                    DepartmentName = x.Department.DepartmentName
                } : null
            })
            .ToListAsync();
        return Ok(result);
    }

    [HttpGet("employee/{employeeId}")]
    public async Task<IActionResult> GetByEmployee(string employeeId)
    {
        if (!await _accessScope.CanViewEmployeeAsync(User, employeeId))
            return Forbid();

        if (!await _context.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId))
            return NotFound("Employee not found.");

        var result = await _context.EmployeeDepartments
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .Select(x => new EmployeeDepartmentByEmployeeResponse
            {
                EmployeeId = x.EmployeeId,
                DepartmentId = x.DepartmentId,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Department = x.Department != null ? new DepartmentBasicDto
                {
                    DepartmentId = x.Department.DepartmentId,
                    DepartmentName = x.Department.DepartmentName
                } : null
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("department/{departmentId}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> GetByDepartment(string departmentId)
    {
        if (!await _context.Departments.AsNoTracking().AnyAsync(x => x.DepartmentId == departmentId))
            return NotFound("Department not found.");

        var result = await _context.EmployeeDepartments
            .AsNoTracking()
            .Where(x => x.DepartmentId == departmentId)
            .Select(x => new EmployeeDepartmentByDepartmentResponse
            {
                EmployeeId = x.EmployeeId,
                DepartmentId = x.DepartmentId,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Employee = x.Employee != null ? new EmployeeBasicDto
                {
                    EmployeeId = x.Employee.EmployeeId,
                    FirstName = x.Employee.FirstName,
                    LastName = x.Employee.LastName,
                    Email = x.Employee.Email,
                    PhoneNumber = x.Employee.PhoneNumber,
                    AccountId = x.Employee.AccountId,
                    WorkNormId = x.Employee.WorkNormId
                } : null,
                Department = x.Department != null ? new DepartmentBasicDto
                {
                    DepartmentId = x.Department.DepartmentId,
                    DepartmentName = x.Department.DepartmentName
                } : null
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Create(EmployeeDepartmentRequest request)
    {
        if (!await _accessScope.CanManageEmployeeAsync(User, request.EmployeeId))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.EmployeeId) || string.IsNullOrWhiteSpace(request.DepartmentId))
            return BadRequest("EmployeeId and DepartmentId are required.");

        if (request.StartDate == default)
            return BadRequest("StartDate is required.");

        if (request.EndDate.HasValue && request.EndDate.Value < request.StartDate)
            return BadRequest("EndDate cannot be before StartDate.");

        if (!await _context.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == request.EmployeeId))
            return BadRequest("Selected employee does not exist.");

        if (!await _context.Departments.AsNoTracking().AnyAsync(x => x.DepartmentId == request.DepartmentId))
            return BadRequest("Selected department does not exist.");

        var exists = await _context.EmployeeDepartments.AsNoTracking().AnyAsync(x => x.EmployeeId == request.EmployeeId && x.DepartmentId == request.DepartmentId);
        if (exists)
            return BadRequest("Employee is already assigned to this department.");

        var item = new EmployeeDepartment
        {
            EmployeeId = request.EmployeeId,
            DepartmentId = request.DepartmentId,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        _context.EmployeeDepartments.Add(item);
        await _context.SaveChangesAsync();

        var response = new EmployeeDepartmentResponse
        {
            EmployeeId = item.EmployeeId,
            DepartmentId = item.DepartmentId,
            StartDate = item.StartDate,
            EndDate = item.EndDate
        };

        return CreatedAtAction(nameof(GetByEmployee), new { employeeId = item.EmployeeId }, response);
    }

    [HttpDelete("{employeeId}/{departmentId}")]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Delete(string employeeId, string departmentId)
    {
        if (!await _accessScope.CanManageEmployeeAsync(User, employeeId))
            return Forbid();

        var item = await _context.EmployeeDepartments.FindAsync(employeeId, departmentId);
        if (item == null)
            return NotFound("Employee department assignment not found.");

        _context.EmployeeDepartments.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
