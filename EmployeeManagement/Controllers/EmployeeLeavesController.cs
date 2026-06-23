using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeeLeavesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAllocationService _allocationService;
    private readonly IAccessScopeService _accessScope;
    private readonly IAuditLogService _audit;

    public EmployeeLeavesController(AppDbContext context, IAllocationService allocationService, IAccessScopeService accessScope, IAuditLogService audit)
    {
        _context = context;
        _allocationService = allocationService;
        _accessScope = accessScope;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!await EmployeeLeaveTableExists()) return Ok(new List<EmployeeLeaveDto>());

        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT l.EmployeeLeaveId, l.EmployeeId, e.FirstName + ' ' + e.LastName AS EmployeeName, l.StartDate, l.EndDate, l.LeaveType, l.Reason, l.ReplacementEmployeeId,
       CASE WHEN r.EmployeeId IS NULL THEN NULL ELSE r.FirstName + ' ' + r.LastName END AS ReplacementEmployeeName
FROM EmployeeLeave l
JOIN Employee e ON e.EmployeeId = l.EmployeeId
LEFT JOIN Employee r ON r.EmployeeId = l.ReplacementEmployeeId
ORDER BY l.StartDate";
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        var result = new List<EmployeeLeaveDto>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) result.Add(ReadLeave(reader));
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            return Ok(result);
        }
        if (User.IsInRole(RoleNames.Admin))
            return Ok(result);

        var visible = new List<EmployeeLeaveDto>();
        foreach (var leave in result)
        {
            if (await _accessScope.CanViewEmployeeAsync(User, leave.EmployeeId))
                visible.Add(leave);
        }
        return Ok(visible);
    }

    [HttpPost]
    public async Task<IActionResult> Create(EmployeeLeaveCreateDto dto)
    {
        if (!await EmployeeLeaveTableExists())
            return BadRequest("EmployeeLeave table does not exist in the database. Run the employee leave SQL migration before creating leaves.");

        if (string.IsNullOrWhiteSpace(dto.EmployeeId)) return BadRequest("Employee is required.");
        var currentEmployeeId = _accessScope.GetCurrentEmployeeId(User);
        var canCreate = User.IsInRole(RoleNames.Admin) ||
            (User.IsInRole(RoleNames.Manager) && await _accessScope.CanManageEmployeeAsync(User, dto.EmployeeId)) ||
            (User.IsInRole(RoleNames.Employee) && currentEmployeeId == dto.EmployeeId);
        if (!canCreate) return Forbid();
        if (dto.StartDate.Date < DateTime.Today) return BadRequest("Leave start date cannot be in the past.");
        if (dto.StartDate.Date > dto.EndDate.Date) return BadRequest("End date cannot be before start date.");
        if (!await _context.Employees.AnyAsync(e => e.EmployeeId == dto.EmployeeId)) return BadRequest("Employee does not exist.");
        if (!string.IsNullOrWhiteSpace(dto.ReplacementEmployeeId) && !await _context.Employees.AnyAsync(e => e.EmployeeId == dto.ReplacementEmployeeId)) return BadRequest("Replacement employee does not exist.");
        if (dto.ReplacementEmployeeId == dto.EmployeeId) return BadRequest("Replacement cannot be the same employee.");
        var allowedLeaveTypes = new[] { "Vacation", "Medical", "Personal" };
        var leaveType = allowedLeaveTypes.FirstOrDefault(type => string.Equals(type, dto.LeaveType, StringComparison.OrdinalIgnoreCase));
        if (leaveType == null) return BadRequest("Leave type must be Vacation, Medical or Personal.");

        var impactedAllocations = await GetImpactedAllocationsAsync(dto.EmployeeId, dto.StartDate, dto.EndDate);
        if (!string.IsNullOrWhiteSpace(dto.ReplacementEmployeeId))
        {
            var replacementError = await ValidateReplacementCoverageAsync(dto.ReplacementEmployeeId, dto.StartDate, dto.EndDate, impactedAllocations);
            if (replacementError != null) return BadRequest(replacementError);
        }

        var id = string.IsNullOrWhiteSpace(dto.EmployeeLeaveId) ? "LV" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant() : dto.EmployeeLeaveId;
        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
IF EXISTS (SELECT 1 FROM EmployeeLeave WHERE EmployeeId = @EmployeeId AND StartDate <= @EndDate AND EndDate >= @StartDate)
BEGIN
    SELECT 'OVERLAP';
END
ELSE
BEGIN
    INSERT INTO EmployeeLeave (EmployeeLeaveId, EmployeeId, StartDate, EndDate, LeaveType, Reason, ReplacementEmployeeId)
    VALUES (@Id, @EmployeeId, @StartDate, @EndDate, @LeaveType, @Reason, @ReplacementEmployeeId);
    SELECT 'OK';
END";
        Add(command, "@Id", id);
        Add(command, "@EmployeeId", dto.EmployeeId);
        Add(command, "@StartDate", dto.StartDate.Date);
        Add(command, "@EndDate", dto.EndDate.Date);
        Add(command, "@LeaveType", leaveType);
        Add(command, "@Reason", dto.Reason ?? (object)DBNull.Value);
        Add(command, "@ReplacementEmployeeId", string.IsNullOrWhiteSpace(dto.ReplacementEmployeeId) ? DBNull.Value : dto.ReplacementEmployeeId);
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        try
        {
            var status = (await command.ExecuteScalarAsync())?.ToString();
            if (status == "OVERLAP") return BadRequest("Employee already has leave in this period.");
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            return BadRequest("EmployeeLeave table does not exist in the database.");
        }
        await _audit.RecordAsync(User, "Create", "EmployeeLeave", id,
            $"Registered {leaveType.ToLowerInvariant()} leave for {dto.EmployeeId} from {dto.StartDate:yyyy-MM-dd} to {dto.EndDate:yyyy-MM-dd}.",
            after: new { dto.EmployeeId, dto.StartDate, dto.EndDate, LeaveType = leaveType, dto.ReplacementEmployeeId, ImpactedAllocations = impactedAllocations.Count });
        return Ok(new
        {
            employeeLeaveId = id,
            impactedAllocations = impactedAllocations.Count,
            coveredAllocations = string.IsNullOrWhiteSpace(dto.ReplacementEmployeeId) ? 0 : impactedAllocations.Count
        });
    }

    [HttpGet("{leaveId}/impact")]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
    public async Task<IActionResult> GetImpact(string leaveId)
    {
        var plan = await BuildLeavePlanAsync(leaveId);
        return plan == null ? NotFound() : Ok(plan.Impacts);
    }

    [HttpGet("{leaveId}/plan")]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
    public async Task<IActionResult> GetPlan(string leaveId)
    {
        var plan = await BuildLeavePlanAsync(leaveId);
        return plan == null ? NotFound() : Ok(plan);
    }

    private async Task<bool> EmployeeLeaveTableExists()
    {
        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN OBJECT_ID('EmployeeLeave', 'U') IS NULL THEN 0 ELSE 1 END";
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) == 1;
    }

    private async Task<EmployeeLeavePlanDto?> BuildLeavePlanAsync(string leaveId)
    {
        if (!await EmployeeLeaveTableExists())
            return null;

        var leave = await GetLeaveByIdAsync(leaveId);
        if (leave == null)
            return null;

        var allocations = await GetImpactedAllocationsAsync(leave.EmployeeId, leave.StartDate, leave.EndDate);

        var impacts = new List<EmployeeLeaveImpactDto>();
        foreach (var allocation in allocations)
        {
            var allocationEnd = allocation.AllocationEndDate ?? allocation.AllocationStartDate;
            var overlapStart = allocation.AllocationStartDate.Date > leave.StartDate.Date ? allocation.AllocationStartDate.Date : leave.StartDate.Date;
            var overlapEnd = allocationEnd.Date < leave.EndDate.Date ? allocationEnd.Date : leave.EndDate.Date;
            var availability = await _allocationService.GetAvailabilityAsync(new AllocationAvailabilityRequest
            {
                ProjectId = allocation.ProjectId,
                SkillId = allocation.TaskItem?.RequiredSkillId,
                StartDate = overlapStart,
                EndDate = overlapEnd,
                RequiredHoursPerDay = allocation.AllocatedHours
            });

            var candidates = availability
                .Where(c => c.EmployeeId != leave.EmployeeId && c.CanTakeRequestedHours)
                .OrderBy(c => leave.ReplacementEmployeeId == c.EmployeeId ? 0 : 1)
                .ThenBy(c => c.ExistingAllocatedHours)
                .ThenByDescending(c => c.MinimumDailyAvailableHours)
                .Take(5)
                .ToList();

            var selectedReplacementIsValid = !string.IsNullOrWhiteSpace(leave.ReplacementEmployeeId);

            impacts.Add(new EmployeeLeaveImpactDto
            {
                ProjectId = allocation.ProjectId,
                ProjectName = allocation.Project?.ProjectName ?? allocation.ProjectId,
                TaskId = allocation.TaskId,
                TaskName = allocation.TaskItem?.TaskName ?? allocation.TaskId,
                AllocationStartDate = allocation.AllocationStartDate,
                AllocationEndDate = allocation.AllocationEndDate,
                OverlapStartDate = overlapStart,
                OverlapEndDate = overlapEnd,
                AllocatedHours = allocation.AllocatedHours,
                RequiredSkillId = allocation.TaskItem?.RequiredSkillId,
                RequiredSkillName = allocation.TaskItem?.RequiredSkill?.SkillName,
                RequiredSkillLevel = allocation.TaskItem?.RequiredSkill?.SkillLevel,
                Status = selectedReplacementIsValid
                    ? "Covered by replacement"
                    : candidates.Any()
                        ? "Replacement available"
                        : "Delay risk",
                ReplacementCandidates = candidates
            });
        }

        var hasDelayRisk = impacts.Any(i => i.Status == "Delay risk");
        return new EmployeeLeavePlanDto
        {
            Leave = leave,
            HasDelayRisk = hasDelayRisk,
            Recommendation = impacts.Count == 0
                ? "No active allocations overlap this leave."
                : !string.IsNullOrWhiteSpace(leave.ReplacementEmployeeId)
                    ? $"All impacted allocations are covered by {leave.ReplacementEmployeeName ?? leave.ReplacementEmployeeId}."
                : hasDelayRisk
                    ? "At least one impacted task has no qualified available replacement. Replan the task or reduce scope."
                    : "Qualified replacements are available for all impacted allocations.",
            Impacts = impacts
        };
    }

    private async Task<List<Entities.Allocation>> GetImpactedAllocationsAsync(string employeeId, DateTime startDate, DateTime endDate)
    {
        return await _context.Allocations
            .AsNoTracking()
            .Include(allocation => allocation.Project)
            .Include(allocation => allocation.TaskItem)
                .ThenInclude(task => task!.RequiredSkill)
            .Where(allocation => allocation.EmployeeId == employeeId &&
                allocation.AllocationStartDate.Date <= endDate.Date &&
                (allocation.AllocationEndDate ?? allocation.AllocationStartDate).Date >= startDate.Date)
            .OrderBy(allocation => allocation.AllocationStartDate)
            .ToListAsync();
    }

    private async Task<string?> ValidateReplacementCoverageAsync(string replacementEmployeeId, DateTime startDate, DateTime endDate, List<Entities.Allocation> impactedAllocations)
    {
        foreach (var allocation in impactedAllocations)
        {
            if (!await _allocationService.EmployeeMeetsSkillRequirementAsync(replacementEmployeeId, allocation.TaskItem?.RequiredSkillId))
                return $"Selected replacement does not meet the skill requirement for task {allocation.TaskItem?.TaskName ?? allocation.TaskId}.";
        }

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            var requiredHours = impactedAllocations
                .Where(allocation => allocation.AllocationStartDate.Date <= date &&
                    (allocation.AllocationEndDate ?? allocation.AllocationStartDate).Date >= date)
                .Sum(allocation => allocation.AllocatedHours);
            if (requiredHours <= 0)
                continue;

            var availability = await _allocationService.GetAvailabilityAsync(new AllocationAvailabilityRequest
            {
                EmployeeId = replacementEmployeeId,
                StartDate = date,
                EndDate = date,
                RequiredHoursPerDay = requiredHours
            });
            var candidate = availability.SingleOrDefault();
            if (candidate == null || !candidate.CanTakeRequestedHours)
                return $"Selected replacement cannot cover {requiredHours:0.##}h on {date:yyyy-MM-dd} without exceeding their work norm or leave schedule.";
        }

        return null;
    }

    private async Task<EmployeeLeaveDto?> GetLeaveByIdAsync(string leaveId)
    {
        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT l.EmployeeLeaveId, l.EmployeeId, e.FirstName + ' ' + e.LastName AS EmployeeName, l.StartDate, l.EndDate, l.LeaveType, l.Reason, l.ReplacementEmployeeId,
       CASE WHEN r.EmployeeId IS NULL THEN NULL ELSE r.FirstName + ' ' + r.LastName END AS ReplacementEmployeeName
FROM EmployeeLeave l
JOIN Employee e ON e.EmployeeId = l.EmployeeId
LEFT JOIN Employee r ON r.EmployeeId = l.ReplacementEmployeeId
WHERE l.EmployeeLeaveId = @LeaveId";
        Add(command, "@LeaveId", leaveId);
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

        try
        {
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? ReadLeave(reader) : null;
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            return null;
        }
    }

    private static EmployeeLeaveDto ReadLeave(System.Data.Common.DbDataReader reader) => new()
    {
        EmployeeLeaveId = reader.GetString(0),
        EmployeeId = reader.GetString(1),
        EmployeeName = reader.GetString(2),
        StartDate = reader.GetDateTime(3),
        EndDate = reader.GetDateTime(4),
        LeaveType = reader.GetString(5),
        Reason = reader.IsDBNull(6) ? null : reader.GetString(6),
        ReplacementEmployeeId = reader.IsDBNull(7) ? null : reader.GetString(7),
        ReplacementEmployeeName = reader.IsDBNull(8) ? null : reader.GetString(8)
    };

    private static void Add(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
