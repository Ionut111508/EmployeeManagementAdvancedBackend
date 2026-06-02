using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    public async Task<IActionResult> Create(CreateAllocationRequest request)
    {
        var endDate = request.AllocationEndDate ?? request.AllocationStartDate;
        if (await IsEmployeeOnLeaveAsync(request.EmployeeId, request.AllocationStartDate, endDate))
            return BadRequest("Employee is on leave in this period. Select another employee or delay the task.");

        var result = await _service.CreateAllocationAsync(request);
        return result.Success ? Ok(result.Allocation) : BadRequest(result.Error);
    }

    [HttpPost("auto")]
    public async Task<IActionResult> CreateAuto(AutoAllocationRequest request)
    {
        var endDate = request.EndDate ?? request.StartDate;
        if (request.StartDate.Date > endDate.Date || request.HoursPerDay <= 0) return BadRequest("Invalid interval or hours.");

        var task = await _context.TaskItems
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ProjectId == request.ProjectId && t.TaskId == request.TaskId);
        if (task == null) return BadRequest("Task does not exist.");

        var effectiveSkillId = string.IsNullOrWhiteSpace(request.SkillId) ? task.RequiredSkillId : request.SkillId;
        var candidates = await _service.GetAvailabilityAsync(new AllocationAvailabilityRequest
        {
            ProjectId = request.ProjectId,
            SkillId = effectiveSkillId,
            StartDate = request.StartDate.Date,
            EndDate = endDate.Date,
            RequiredHoursPerDay = request.HoursPerDay
        });

        var selectedEmployeeId = candidates
            .Where(c => c.CanTakeRequestedHours)
            .OrderBy(c => c.ExistingAllocatedHours)
            .ThenByDescending(c => c.MinimumDailyAvailableHours)
            .ThenBy(c => c.FullName)
            .Select(c => c.EmployeeId)
            .FirstOrDefault();

        if (selectedEmployeeId == null) return BadRequest("No available employee found for this interval. The task should be delayed or assigned manually to a replacement.");

        var result = await _service.CreateAllocationAsync(new CreateAllocationRequest
        {
            EmployeeId = selectedEmployeeId,
            ProjectId = request.ProjectId,
            TaskId = request.TaskId,
            AllocationStartDate = request.StartDate,
            AllocationEndDate = endDate,
            AllocatedHours = request.HoursPerDay
        });

        return result.Success ? Ok(result.Allocation) : BadRequest(result.Error);
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
}
