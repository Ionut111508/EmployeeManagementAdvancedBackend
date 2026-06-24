using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAccessScopeService _accessScope;
    private readonly IAllocationService _allocationService;

    public NotificationsController(AppDbContext context, IAccessScopeService accessScope, IAllocationService allocationService)
    {
        _context = context;
        _accessScope = accessScope;
        _allocationService = allocationService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationResponse>>> Get([FromQuery] int horizonDays = 30)
    {
        horizonDays = Math.Clamp(horizonDays, 1, 90);
        var projectIds = User.IsInRole(RoleNames.Manager)
            ? await _accessScope.GetManagedProjectIdsAsync(User)
            : await _context.Projects.Select(project => project.ProjectId).ToListAsync();
        var today = DateTime.Today;
        var horizonEnd = today.AddDays(horizonDays);
        var notifications = new List<NotificationResponse>();

        var tasks = await _context.TaskItems.AsNoTracking()
            .Include(task => task.Project)
            .Include(task => task.Allocations)
            .Include(task => task.Timesheets)
            .Where(task => projectIds.Contains(task.ProjectId) && task.Status != TaskStatuses.Completed && task.Status != TaskStatuses.Cancelled)
            .ToListAsync();
        foreach (var task in tasks)
        {
            var approvedWorkedHours = task.Timesheets
                .Where(item => item.Status == TimesheetStatuses.Approved)
                .Sum(item => item.WorkedHours);
            if (TaskStatuses.Resolve(task.Status, task.PlannedEndDate, task.EstimatedHours, approvedWorkedHours, today) == TaskStatuses.Delayed)
            {
                var remainingWork = Math.Max((task.EstimatedHours ?? 0) - approvedWorkedHours, 0);
                notifications.Add(new NotificationResponse
                {
                    NotificationId = $"delayed:{task.ProjectId}:{task.TaskId}",
                    Type = "TaskDelayed",
                    Severity = "Critical",
                    Title = $"{task.TaskName} is delayed",
                    Message = $"{remainingWork:0.##}h remain after the {task.PlannedEndDate:dd.MM.yyyy} deadline.",
                    ProjectId = task.ProjectId,
                    TaskId = task.TaskId,
                    RelevantDate = task.PlannedEndDate
                });
                continue;
            }

            var allocated = 0m;
            foreach (var allocation in task.Allocations)
            {
                allocated += await _allocationService.CalculateEffectiveAllocationHoursAsync(
                    allocation.EmployeeId,
                    allocation.AllocationStartDate,
                    allocation.AllocationEndDate ?? allocation.AllocationStartDate,
                    allocation.AllocatedHours);
            }
            var missing = Math.Max((task.EstimatedHours ?? 0) - allocated, 0);
            if (missing <= 0.05m) continue;
            notifications.Add(new NotificationResponse
            {
                NotificationId = $"deficit:{task.ProjectId}:{task.TaskId}",
                Type = "StaffingDeficit",
                Severity = task.PlannedStartDate <= today ? "Critical" : "Warning",
                Title = $"{task.TaskName} needs more capacity",
                Message = $"{missing:0.##}h are not allocated from the estimated {task.EstimatedHours:0.##}h.",
                ProjectId = task.ProjectId,
                TaskId = task.TaskId,
                RelevantDate = task.PlannedStartDate
            });
        }

        var employees = await _context.Employees.AsNoTracking().Include(employee => employee.WorkNorm).ToListAsync();
        var allocations = await _context.Allocations.AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId) && item.AllocationStartDate <= horizonEnd && (item.AllocationEndDate ?? item.AllocationStartDate) >= today)
            .ToListAsync();
        foreach (var employee in employees)
        {
            var employeeAllocations = allocations.Where(item => item.EmployeeId == employee.EmployeeId).ToList();
            for (var date = today; date <= horizonEnd; date = date.AddDays(1))
            {
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                var hours = employeeAllocations.Where(item => item.AllocationStartDate.Date <= date && (item.AllocationEndDate ?? item.AllocationStartDate).Date >= date).Sum(item => item.AllocatedHours);
                var norm = employee.WorkNorm?.WorkHours ?? 0;
                if (hours <= norm + 0.01m) continue;
                notifications.Add(new NotificationResponse
                {
                    NotificationId = $"overload:{employee.EmployeeId}:{date:yyyyMMdd}",
                    Type = "OverAllocation",
                    Severity = "Critical",
                    Title = $"{employee.FirstName} {employee.LastName} is overallocated",
                    Message = $"{hours:0.##}h allocated on {date:dd.MM.yyyy}, above the {norm:0.##}h work norm.",
                    EmployeeId = employee.EmployeeId,
                    RelevantDate = date
                });
            }
        }

        var pendingCount = await _context.Timesheets.CountAsync(item => projectIds.Contains(item.ProjectId) && item.Status == TimesheetStatuses.Pending);
        if (pendingCount > 0)
        {
            notifications.Add(new NotificationResponse
            {
                NotificationId = "timesheets:pending",
                Type = "TimesheetApproval",
                Severity = "Info",
                Title = "Timesheets waiting for approval",
                Message = $"{pendingCount} timesheet entries require review."
            });
        }

        return Ok(notifications.OrderBy(item => item.Severity == "Critical" ? 0 : item.Severity == "Warning" ? 1 : 2).ThenBy(item => item.RelevantDate).ToList());
    }
}
