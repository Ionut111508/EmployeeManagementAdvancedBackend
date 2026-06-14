using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAllocationService _allocationService;
    private readonly IAccessScopeService _accessScope;

    public DashboardController(AppDbContext context, IAllocationService allocationService, IAccessScopeService accessScope)
    {
        _context = context;
        _allocationService = allocationService;
        _accessScope = accessScope;
    }

    [HttpGet("employee/{employeeId}/workload")]
    public async Task<IActionResult> GetEmployeeWorkload(string employeeId)
    {
        if (!await _accessScope.CanViewEmployeeAsync(User, employeeId))
            return Forbid();

        var employee = await _context.Employees.Include(x => x.WorkNorm).FirstOrDefaultAsync(x => x.EmployeeId == employeeId);
        if (employee == null) return NotFound();

        var allocations = await _allocationService.GetByEmployeeAsync(employeeId);
        foreach (var allocation in allocations)
        {
            var start = allocation.AllocationStartDate ?? DateTime.Today;
            var end = allocation.AllocationEndDate ?? start;
            allocation.TotalAllocationHours = _allocationService.CalculateTotalAllocationHours(start, end, allocation.AllocatedHours);
        }

        var today = DateTime.Today;
        var workedHours = await _context.Timesheets.Where(x => x.EmployeeId == employeeId).SumAsync(x => x.WorkedHours);

        var response = new EmployeeWorkloadResponse
        {
            EmployeeId = employee.EmployeeId,
            FullName = employee.LastName + " " + employee.FirstName,
            WorkNormHours = employee.WorkNorm?.WorkHours ?? 0,
            PastAllocations = allocations.Where(x => (x.AllocationEndDate ?? x.AllocationStartDate ?? today).Date < today).ToList(),
            CurrentAllocations = allocations.Where(x => (x.AllocationStartDate ?? today).Date <= today && (x.AllocationEndDate ?? x.AllocationStartDate ?? today).Date >= today).ToList(),
            FutureAllocations = allocations.Where(x => (x.AllocationStartDate ?? today).Date > today).ToList(),
            TotalAllocatedHours = allocations.Sum(x => x.TotalAllocationHours),
            TotalWorkedHours = workedHours,
            NumberOfProjects = allocations.Select(x => x.ProjectId).Distinct().Count(),
            NumberOfTasks = allocations.Select(x => new { x.ProjectId, x.TaskId }).Distinct().Count()
        };

        return Ok(response);
    }

    [HttpGet("task-progress/{projectId}/{taskId}")]
    public async Task<IActionResult> GetTaskProgress(string projectId, string taskId)
    {
        if (!await _accessScope.CanViewTaskAsync(User, projectId, taskId))
            return Forbid();

        var task = await _context.TaskItems.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.TaskId == taskId);
        if (task == null) return NotFound();

        var allocations = await _context.Allocations.Where(x => x.ProjectId == projectId && x.TaskId == taskId).ToListAsync();
        var allocatedHours = allocations.Sum(x => _allocationService.CalculateTotalAllocationHours(x.AllocationStartDate, x.AllocationEndDate ?? x.AllocationStartDate, x.AllocatedHours));
        var workedHours = await _context.Timesheets.Where(x => x.ProjectId == projectId && x.TaskId == taskId).SumAsync(x => x.WorkedHours);
        var estimated = task.EstimatedHours ?? 0;

        return Ok(new TaskProgressResponse
        {
            ProjectId = projectId,
            TaskId = taskId,
            TaskName = task.TaskName,
            EstimatedHours = estimated,
            AllocatedHours = allocatedHours,
            WorkedHours = workedHours,
            AllocationProgressPercentage = estimated <= 0 ? 0 : Math.Round(allocatedHours / estimated * 100, 2),
            WorkedProgressPercentage = estimated <= 0 ? 0 : Math.Round(workedHours / estimated * 100, 2)
        });
    }

    [HttpGet("project/{projectId}/summary")]
    public async Task<IActionResult> GetProjectSummary(string projectId)
    {
        if (!await _accessScope.CanViewProjectAsync(User, projectId))
            return Forbid();

        var project = await _context.Projects.FirstOrDefaultAsync(x => x.ProjectId == projectId);
        if (project == null) return NotFound();

        var tasks = await _context.TaskItems.Where(x => x.ProjectId == projectId).ToListAsync();
        var allocations = await _context.Allocations.Where(x => x.ProjectId == projectId).ToListAsync();
        var workedHours = await _context.Timesheets.Where(x => x.ProjectId == projectId).SumAsync(x => x.WorkedHours);
        var estimatedHours = tasks.Sum(x => x.EstimatedHours ?? 0);
        var allocatedHours = allocations.Sum(x => _allocationService.CalculateTotalAllocationHours(x.AllocationStartDate, x.AllocationEndDate ?? x.AllocationStartDate, x.AllocatedHours));

        return Ok(new ProjectSummaryResponse
        {
            ProjectId = project.ProjectId,
            ProjectName = project.ProjectName,
            NumberOfTasks = tasks.Count,
            TotalEstimatedHours = estimatedHours,
            TotalAllocatedHours = allocatedHours,
            TotalWorkedHours = workedHours,
            ProgressPercentage = estimatedHours <= 0 ? 0 : Math.Round(workedHours / estimatedHours * 100, 2)
        });
    }
}
