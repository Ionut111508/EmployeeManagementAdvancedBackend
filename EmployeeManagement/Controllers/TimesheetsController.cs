using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Services;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TimesheetsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAccessScopeService _accessScope;

    public TimesheetsController(AppDbContext context, IAccessScopeService accessScope)
    {
        _context = context;
        _accessScope = accessScope;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        IQueryable<Timesheet> query = _context.Timesheets.AsNoTracking();
        if (User.IsInRole(RoleNames.Employee))
        {
            var employeeId = _accessScope.GetCurrentEmployeeId(User);
            query = query.Where(t => t.EmployeeId == employeeId);
        }
        else if (User.IsInRole(RoleNames.Manager))
        {
            var projectIds = await _accessScope.GetManagedProjectIdsAsync(User);
            query = query.Where(t => projectIds.Contains(t.ProjectId));
        }
        return Ok(await query.OrderByDescending(t => t.WorkDate).ToListAsync());
    }

    [HttpGet("employee/{employeeId}")]
    public async Task<IActionResult> GetByEmployee(string employeeId)
    {
        if (!await _accessScope.CanViewEmployeeAsync(User, employeeId))
            return Forbid();

        var projectIds = User.IsInRole(RoleNames.Manager)
            ? await _accessScope.GetManagedProjectIdsAsync(User)
            : Array.Empty<string>();
        var result = await _context.Timesheets
            .Where(x => x.EmployeeId == employeeId &&
                (!User.IsInRole(RoleNames.Manager) || projectIds.Contains(x.ProjectId)))
            .OrderByDescending(x => x.WorkDate)
            .ToListAsync();
        return Ok(result);
    }

    [HttpGet("task/{projectId}/{taskId}")]
    public async Task<IActionResult> GetByTask(string projectId, string taskId)
    {
        if (!await _accessScope.CanViewTaskAsync(User, projectId, taskId))
            return Forbid();

        var result = await _context.Timesheets
            .Where(x => x.ProjectId == projectId && x.TaskId == taskId)
            .OrderByDescending(x => x.WorkDate)
            .ToListAsync();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Add(TimesheetRequest request)
    {
        if (request.WorkedHours <= 0) return BadRequest("Invalid hours.");

        var currentEmployeeId = _accessScope.GetCurrentEmployeeId(User);
        if (!User.IsInRole(RoleNames.Admin) && currentEmployeeId != request.EmployeeId)
            return Forbid();

        var employee = await _context.Employees
            .Include(x => x.WorkNorm)
            .FirstOrDefaultAsync(x => x.EmployeeId == request.EmployeeId);
        if (employee == null) return BadRequest("Invalid employee.");

        var taskExists = await _context.TaskItems.AnyAsync(x => x.ProjectId == request.ProjectId && x.TaskId == request.TaskId);
        if (!taskExists) return BadRequest("Invalid task.");

        var workDate = request.WorkDate.Date;
        var allocation = await _context.Allocations.AsNoTracking().FirstOrDefaultAsync(x =>
            x.EmployeeId == request.EmployeeId && x.ProjectId == request.ProjectId && x.TaskId == request.TaskId &&
            x.AllocationStartDate.Date <= workDate && (x.AllocationEndDate ?? x.AllocationStartDate).Date >= workDate);
        if (allocation == null)
            return BadRequest("Employee is not allocated to this task on the selected date.");

        var existing = await _context.Timesheets.FindAsync(request.ProjectId, request.TaskId, request.EmployeeId, workDate);
        var taskHoursAfterUpdate = (existing?.WorkedHours ?? 0) + request.WorkedHours;
        if (taskHoursAfterUpdate > allocation.AllocatedHours)
            return BadRequest($"Reported task hours cannot exceed the {allocation.AllocatedHours:0.##} allocated hours for this day.");

        var dailyHours = await _context.Timesheets
            .Where(x => x.EmployeeId == request.EmployeeId && x.WorkDate == workDate)
            .SumAsync(x => x.WorkedHours);
        var workNormHours = employee.WorkNorm?.WorkHours ?? 0;
        if (dailyHours + request.WorkedHours > workNormHours)
            return BadRequest($"Daily reported hours cannot exceed the {workNormHours:0.##} hour work norm.");

        if (existing == null)
        {
            existing = new Timesheet
            {
                ProjectId = request.ProjectId,
                TaskId = request.TaskId,
                EmployeeId = request.EmployeeId,
                WorkDate = workDate,
                WorkedHours = request.WorkedHours
            };
            _context.Timesheets.Add(existing);
        }
        else
        {
            existing.WorkedHours += request.WorkedHours;
        }

        await _context.SaveChangesAsync();
        return Ok(existing);
    }
}
