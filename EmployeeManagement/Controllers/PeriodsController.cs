using EmployeeManagement.Data;
using EmployeeManagement.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Services;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PeriodsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAccessScopeService _accessScope;

    public PeriodsController(AppDbContext context, IAccessScopeService accessScope)
    {
        _context = context;
        _accessScope = accessScope;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (User.IsInRole(RoleNames.Admin))
            return Ok(await _context.Periods.ToListAsync());

        var currentEmployeeId = _accessScope.GetCurrentEmployeeId(User);
        if (User.IsInRole(RoleNames.Employee))
            return Ok(await _context.Periods.Where(period => period.EmployeeId == currentEmployeeId).ToListAsync());

        var projectIds = await _accessScope.GetManagedProjectIdsAsync(User);
        return Ok(await _context.Periods.Where(period =>
            period.EmployeeId == currentEmployeeId ||
            _context.Allocations.Any(allocation => allocation.EmployeeId == period.EmployeeId && projectIds.Contains(allocation.ProjectId)))
            .ToListAsync());
    }

    [HttpGet("employee/{employeeId}")]
    public async Task<IActionResult> GetByEmployee(string employeeId)
    {
        if (!await _accessScope.CanViewEmployeeAsync(User, employeeId))
            return Forbid();
        return Ok(await _context.Periods.Where(x => x.EmployeeId == employeeId).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create(Period period)
    {
        var canCreate = User.IsInRole(RoleNames.Admin) ||
            _accessScope.GetCurrentEmployeeId(User) == period.EmployeeId ||
            await _accessScope.CanManageEmployeeAsync(User, period.EmployeeId);
        if (!canCreate) return Forbid();

        if (!await _context.Employees.AnyAsync(x => x.EmployeeId == period.EmployeeId)) return BadRequest("Invalid employee.");
        _context.Periods.Add(period);
        await _context.SaveChangesAsync();
        return Ok(period);
    }

    [HttpDelete("{periodId}")]
    public async Task<IActionResult> Delete(string periodId)
    {
        var period = await _context.Periods.FindAsync(periodId);
        if (period == null) return NotFound();
        var canDelete = User.IsInRole(RoleNames.Admin) ||
            _accessScope.GetCurrentEmployeeId(User) == period.EmployeeId ||
            await _accessScope.CanManageEmployeeAsync(User, period.EmployeeId);
        if (!canDelete) return Forbid();
        _context.Periods.Remove(period);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
