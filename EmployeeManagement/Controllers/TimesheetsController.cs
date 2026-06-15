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
    private readonly IAuditLogService _audit;

    public TimesheetsController(AppDbContext context, IAccessScopeService accessScope, IAuditLogService audit)
    {
        _context = context;
        _accessScope = accessScope;
        _audit = audit;
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
                WorkedHours = request.WorkedHours,
                Status = TimesheetStatuses.Pending,
                SubmittedAt = DateTime.UtcNow
            };
            _context.Timesheets.Add(existing);
        }
        else
        {
            existing.WorkedHours += request.WorkedHours;
            existing.Status = TimesheetStatuses.Pending;
            existing.SubmittedAt = DateTime.UtcNow;
            existing.ReviewedAt = null;
            existing.ReviewedByEmployeeId = null;
            existing.ReviewComment = null;
        }

        await _context.SaveChangesAsync();
        await _audit.RecordAsync(User, "Submit", "Timesheet", $"{request.ProjectId}/{request.TaskId}/{request.EmployeeId}/{workDate:yyyy-MM-dd}", $"Submitted {request.WorkedHours:0.##}h for approval.", request.ProjectId, after: new { existing.WorkedHours, existing.Status });
        return Ok(ToResponse(existing));
    }

    [HttpPut("{projectId}/{taskId}/{employeeId}/{workDate}/review")]
    public async Task<IActionResult> Review(string projectId, string taskId, string employeeId, DateTime workDate, TimesheetReviewRequest request)
    {
        if (!User.IsInRole(RoleNames.Admin) && !User.IsInRole(RoleNames.Manager)) return Forbid();
        if (!await _accessScope.CanManageProjectAsync(User, projectId)) return Forbid();
        if (request.Status is not TimesheetStatuses.Approved and not TimesheetStatuses.Rejected)
            return BadRequest("Status must be Approved or Rejected.");

        var entry = await _context.Timesheets.FindAsync(projectId, taskId, employeeId, workDate.Date);
        if (entry == null) return NotFound();
        var before = new { entry.Status, entry.ReviewComment };
        entry.Status = request.Status;
        entry.ReviewComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        entry.ReviewedAt = DateTime.UtcNow;
        entry.ReviewedByEmployeeId = _accessScope.GetCurrentEmployeeId(User);
        await _context.SaveChangesAsync();
        await _audit.RecordAsync(User, request.Status, "Timesheet", $"{projectId}/{taskId}/{employeeId}/{workDate:yyyy-MM-dd}", $"Timesheet entry was {request.Status.ToLowerInvariant()}.", projectId, before, new { entry.Status, entry.ReviewComment });
        return Ok(ToResponse(entry));
    }

    private static TimesheetResponse ToResponse(Timesheet item) => new()
    {
        ProjectId = item.ProjectId,
        TaskId = item.TaskId,
        EmployeeId = item.EmployeeId,
        WorkDate = item.WorkDate,
        WorkedHours = item.WorkedHours,
        Status = item.Status,
        SubmittedAt = item.SubmittedAt,
        ReviewedAt = item.ReviewedAt,
        ReviewedByEmployeeId = item.ReviewedByEmployeeId,
        ReviewComment = item.ReviewComment
    };
}
