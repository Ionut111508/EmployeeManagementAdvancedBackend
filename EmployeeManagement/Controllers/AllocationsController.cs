using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AllocationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAllocationService _service;

    public AllocationsController(AppDbContext context, IAllocationService service)
    {
        _context = context;
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("employee/{employeeId}")]
    public async Task<IActionResult> GetByEmployee(string employeeId) => Ok(await _service.GetByEmployeeAsync(employeeId));

    [HttpGet("project/{projectId}")]
    public async Task<IActionResult> GetByProject(string projectId) => Ok(await _service.GetByProjectAsync(projectId));

    [HttpGet("task/{projectId}/{taskId}")]
    public async Task<IActionResult> GetByTask(string projectId, string taskId) => Ok(await _service.GetByTaskAsync(projectId, taskId));

    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability([FromQuery] AllocationAvailabilityRequest request)
    {
        var endDate = request.EndDate ?? request.StartDate;
        if (request.StartDate == default)
            return BadRequest("StartDate is required.");
        if (request.StartDate.Date > endDate.Date)
            return BadRequest("EndDate cannot be before StartDate.");
        if (request.RequiredHoursPerDay.HasValue && request.RequiredHoursPerDay.Value <= 0)
            return BadRequest("RequiredHoursPerDay must be greater than zero.");

        return Ok(await _service.GetAvailabilityAsync(request));
    }

    [HttpGet("underutilized")]
    public async Task<IActionResult> GetUnderutilized([FromQuery] AllocationAvailabilityRequest request)
    {
        var endDate = request.EndDate ?? request.StartDate;
        if (request.StartDate == default)
            return BadRequest("StartDate is required.");
        if (request.StartDate.Date > endDate.Date)
            return BadRequest("EndDate cannot be before StartDate.");

        var result = await _service.GetAvailabilityAsync(request);
        return Ok(result
            .Where(x => !x.IsOnLeave && x.AvailableHours > 0)
            .OrderByDescending(x => x.MinimumDailyAvailableHours)
            .ThenByDescending(x => x.AvailableHours)
            .ThenBy(x => x.FullName)
            .ToList());
    }

    [HttpGet("planning-overview")]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
    public async Task<IActionResult> GetPlanningOverview([FromQuery] DateTime startDate, [FromQuery] int windowDays = 14, [FromQuery] string? projectId = null)
    {
        if (startDate == default)
            return BadRequest("StartDate is required.");
        if (windowDays < 1 || windowDays > 90)
            return BadRequest("WindowDays must be between 1 and 90.");
        if (!string.IsNullOrWhiteSpace(projectId) && !await CanManageProjectAsync(projectId))
            return Forbid();

        var currentStart = startDate.Date;
        var currentEnd = currentStart.AddDays(windowDays - 1);
        var futureStart = currentEnd.AddDays(1);
        var futureEnd = futureStart.AddDays(windowDays - 1);
        var current = await _service.GetAvailabilityAsync(new AllocationAvailabilityRequest
        {
            ProjectId = projectId,
            StartDate = currentStart,
            EndDate = currentEnd
        });
        var future = await _service.GetAvailabilityAsync(new AllocationAvailabilityRequest
        {
            ProjectId = projectId,
            StartDate = futureStart,
            EndDate = futureEnd
        });
        var currentByEmployee = current.ToDictionary(item => item.EmployeeId);
        var becomingAvailable = future
            .Where(item => !item.IsOnLeave && item.AvailableHours > 0 && currentByEmployee.TryGetValue(item.EmployeeId, out var currentItem) &&
                (item.MinimumDailyAvailableHours > currentItem.MinimumDailyAvailableHours + 0.01m ||
                 item.AvailableHours > currentItem.AvailableHours + 0.01m))
            .OrderByDescending(item => item.MinimumDailyAvailableHours)
            .ThenBy(item => item.FullName)
            .ToList();

        return Ok(new ResourcePlanningOverviewResponse
        {
            CurrentStartDate = currentStart,
            CurrentEndDate = currentEnd,
            FutureStartDate = futureStart,
            FutureEndDate = futureEnd,
            IdleEmployees = current.Where(item => !item.IsOnLeave && item.ExistingAllocatedHours == 0 && item.AvailableHours > 0).ToList(),
            UnderutilizedEmployees = current.Where(item => !item.IsOnLeave && item.ExistingAllocatedHours > 0 && item.AvailableHours > 0).ToList(),
            BecomingAvailableEmployees = becomingAvailable
        });
    }

    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate(AllocationSimulationRequest request)
    {
        var endDate = request.EndDate ?? request.StartDate;
        if (request.StartDate.Date > endDate.Date)
            return BadRequest("EndDate cannot be before StartDate.");
        if (request.HoursPerDay <= 0)
            return BadRequest("HoursPerDay must be greater than zero.");

        var result = await _service.SimulateAllocationAsync(request);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Create(CreateAllocationRequest request)
    {
        if (!await CanManageProjectAsync(request.ProjectId))
            return Forbid();
        var endDate = request.AllocationEndDate ?? request.AllocationStartDate;
        if (await IsEmployeeOnLeaveAsync(request.EmployeeId, request.AllocationStartDate, endDate))
            return BadRequest("Employee is on leave in this period. Select another employee or delay the task.");

        var result = await _service.CreateAllocationAsync(request);
        return result.Success ? Ok(result.Allocation) : BadRequest(result.Error);
    }

    [HttpPost("auto")]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
    public async Task<IActionResult> CreateAuto(AutoAllocationRequest request)
    {
        var task = await _context.TaskItems
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ProjectId == request.ProjectId && t.TaskId == request.TaskId);
        if (task == null) return BadRequest("Task does not exist.");
        if (!await CanManageProjectAsync(request.ProjectId))
            return Forbid();

        var startDate = request.StartDate == default ? task.PlannedStartDate : request.StartDate;
        var endDate = request.EndDate ?? task.PlannedEndDate ?? startDate;
        if (!startDate.HasValue || !endDate.HasValue || startDate.Value.Date > endDate.Value.Date)
            return BadRequest("Invalid interval.");

        var existingAllocations = await _context.Allocations
            .Where(allocation => allocation.ProjectId == request.ProjectId && allocation.TaskId == request.TaskId)
            .ToListAsync();
        var existingHours = existingAllocations.Sum(allocation => _service.CalculateTotalAllocationHours(
            allocation.AllocationStartDate,
            allocation.AllocationEndDate ?? allocation.AllocationStartDate,
            allocation.AllocatedHours));
        var remainingHours = Math.Max((task.EstimatedHours ?? 0) - existingHours, 0);
        if (remainingHours <= 0.05m)
            return Ok(new AutoAllocationResponse { AllocatedHours = 0, RemainingHours = 0, Status = "Fully staffed" });

        var effectiveSkillId = string.IsNullOrWhiteSpace(request.SkillId) ? task.RequiredSkillId : request.SkillId;
        var plan = await _service.BuildTaskPlanAsync(new TaskPlanningPreviewRequest
        {
            ProjectId = request.ProjectId,
            RequiredSkillId = effectiveSkillId,
            EstimatedHours = remainingHours,
            PlannedStartDate = startDate.Value.Date,
            PlannedEndDate = endDate.Value.Date,
            ExcludedEmployeeIds = existingAllocations.Select(allocation => allocation.EmployeeId).ToList()
        });
        if (plan.AutomaticPlan.Count == 0)
            return BadRequest("No qualified employee has safe capacity in this interval.");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var created = new List<AllocationResponse>();
        foreach (var planned in plan.AutomaticPlan)
        {
            var result = await _service.CreateAllocationAsync(new CreateAllocationRequest
            {
                EmployeeId = planned.EmployeeId,
                ProjectId = request.ProjectId,
                TaskId = request.TaskId,
                AllocationStartDate = startDate.Value.Date,
                AllocationEndDate = endDate.Value.Date,
                AllocatedHours = planned.HoursPerDay
            });
            if (!result.Success || result.Allocation == null)
            {
                await transaction.RollbackAsync();
                return BadRequest(result.Error);
            }
            created.Add(result.Allocation);
        }
        await transaction.CommitAsync();

        var allocatedNow = created.Sum(item => item.TotalAllocationHours);
        var finalRemaining = Math.Max(remainingHours - allocatedNow, 0);
        return Ok(new AutoAllocationResponse
        {
            Allocations = created,
            AllocatedHours = allocatedNow,
            RemainingHours = finalRemaining,
            Status = finalRemaining <= 0.05m ? "Fully staffed" : "Partially staffed"
        });
    }

    [HttpDelete("{employeeId}/{projectId}/{taskId}")]
    public async Task<IActionResult> Delete(string employeeId, string projectId, string taskId)
    {
        var allocation = await _context.Allocations.FindAsync(employeeId, projectId, taskId);
        if (allocation == null) return NotFound();
        _context.Allocations.Remove(allocation);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> IsEmployeeOnLeaveAsync(string employeeId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM EmployeeLeave WHERE EmployeeId = @employeeId AND StartDate <= @endDate AND EndDate >= @startDate";
            var employeeParameter = command.CreateParameter();
            employeeParameter.ParameterName = "@employeeId";
            employeeParameter.Value = employeeId;
            command.Parameters.Add(employeeParameter);
            var startParameter = command.CreateParameter();
            startParameter.ParameterName = "@startDate";
            startParameter.Value = startDate.Date;
            command.Parameters.Add(startParameter);
            var endParameter = command.CreateParameter();
            endParameter.ParameterName = "@endDate";
            endParameter.Value = endDate.Date;
            command.Parameters.Add(endParameter);
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
            var value = await command.ExecuteScalarAsync();
            return Convert.ToInt32(value) > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CanManageProjectAsync(string projectId)
    {
        if (User.IsInRole(RoleNames.Admin))
            return true;

        var employeeId = User.FindFirst("employee_id")?.Value;
        return User.IsInRole(RoleNames.Manager) &&
            !string.IsNullOrWhiteSpace(employeeId) &&
            await _context.ProjectManagers.AnyAsync(pm => pm.EmployeeId == employeeId && pm.ProjectId == projectId);
    }
}
