using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Services;

public class AllocationService : IAllocationService
{
    private readonly AppDbContext _context;

    public AllocationService(AppDbContext context)
    {
        _context = context;
    }

    public int CountWorkingDays(DateTime start, DateTime end)
    {
        if (end < start) return 0;
        var count = 0;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                count++;
        }
        return count;
    }

    public decimal CalculateTotalAllocationHours(DateTime start, DateTime end, decimal hoursPerDay)
    {
        return CountWorkingDays(start, end) * hoursPerDay;
    }

    public async Task<decimal> CalculateEffectiveAllocationHoursAsync(string employeeId, DateTime start, DateTime end, decimal hoursPerDay)
    {
        var uncoveredLeavePeriods = await GetUncoveredLeavePeriodsAsync(employeeId, start, end);
        var effectiveDays = 0;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            if (uncoveredLeavePeriods.Any(period => date >= period.Start && date <= period.End))
                continue;
            effectiveDays++;
        }

        return effectiveDays * hoursPerDay;
    }

    public bool DatesOverlap(DateTime start1, DateTime end1, DateTime start2, DateTime end2)
    {
        return start1.Date <= end2.Date && start2.Date <= end1.Date;
    }

    public async Task<decimal> GetEmployeeAllocatedHoursForDateAsync(string employeeId, DateTime date)
    {
        var directlyAllocated = await _context.Allocations
            .Where(a => a.EmployeeId == employeeId &&
                        a.AllocationStartDate.Date <= date.Date &&
                        (a.AllocationEndDate ?? a.AllocationStartDate).Date >= date.Date)
            .SumAsync(a => a.AllocatedHours);

        return directlyAllocated + await GetReplacementCoverageHoursForDateAsync(employeeId, date);
    }

    public async Task<List<AllocationResponse>> GetAllAsync() => await BuildAllocationQuery().ToListAsync();

    public async Task<List<AllocationResponse>> GetByEmployeeAsync(string employeeId) =>
        await BuildAllocationQuery().Where(a => a.EmployeeId == employeeId).ToListAsync();

    public async Task<List<AllocationResponse>> GetByProjectAsync(string projectId) =>
        await BuildAllocationQuery().Where(a => a.ProjectId == projectId).ToListAsync();

    public async Task<List<AllocationResponse>> GetByTaskAsync(string projectId, string taskId) =>
        await BuildAllocationQuery().Where(a => a.ProjectId == projectId && a.TaskId == taskId).ToListAsync();

    public async Task<List<AllocationAvailabilityResponse>> GetAvailabilityAsync(AllocationAvailabilityRequest request)
    {
        var startDate = request.StartDate.Date;
        var endDate = (request.EndDate ?? request.StartDate).Date;
        var projectId = string.IsNullOrWhiteSpace(request.ProjectId) ? null : request.ProjectId;

        var employeesQuery = _context.Employees
            .AsNoTracking()
            .Include(e => e.WorkNorm)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.EmployeeId))
            employeesQuery = employeesQuery.Where(e => e.EmployeeId == request.EmployeeId);

        var requiredSkill = await GetRequiredSkillAsync(request.SkillId);

        if (request.OnlyProjectEmployees && projectId != null)
        {
            employeesQuery = employeesQuery.Where(e =>
                _context.Allocations.Any(a => a.EmployeeId == e.EmployeeId && a.ProjectId == projectId) ||
                _context.ProjectManagers.Any(pm => pm.EmployeeId == e.EmployeeId && pm.ProjectId == projectId));
        }

        var employees = await employeesQuery
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .ToListAsync();

        var projectEmployeeIds = projectId == null
            ? new HashSet<string>()
            : (await _context.Allocations
                .AsNoTracking()
                .Where(a => a.ProjectId == projectId)
                .Select(a => a.EmployeeId)
                .Distinct()
                .ToListAsync()).ToHashSet();

        var projectManagerIds = projectId == null
            ? new HashSet<string>()
            : (await _context.ProjectManagers
                .AsNoTracking()
                .Where(pm => pm.ProjectId == projectId)
                .Select(pm => pm.EmployeeId)
                .Distinct()
                .ToListAsync()).ToHashSet();

        var result = new List<AllocationAvailabilityResponse>();
        foreach (var employee in employees)
        {
            var skillMatch = await GetEmployeeSkillMatchAsync(employee.EmployeeId, requiredSkill);
            var workNormHours = employee.WorkNorm?.WorkHours ?? 0;
            var workingDays = CountWorkingDays(startDate, endDate);
            var existingHours = 0m;
            var minimumDailyAvailable = workingDays == 0 ? 0 : decimal.MaxValue;

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                var dailyAllocatedHours = await GetEmployeeAllocatedHoursForDateAsync(employee.EmployeeId, date);
                existingHours += dailyAllocatedHours;
                minimumDailyAvailable = Math.Min(minimumDailyAvailable, Math.Max(workNormHours - dailyAllocatedHours, 0));
            }

            if (minimumDailyAvailable == decimal.MaxValue)
                minimumDailyAvailable = 0;

            var capacityHours = workingDays * workNormHours;
            var availableHours = Math.Max(capacityHours - existingHours, 0);
            var isOnLeave = await IsEmployeeOnLeaveAsync(employee.EmployeeId, startDate, endDate);
            var canTakeRequestedHours = skillMatch.MeetsRequirement &&
                !isOnLeave &&
                workNormHours > 0 &&
                (!request.RequiredHoursPerDay.HasValue || minimumDailyAvailable >= request.RequiredHoursPerDay.Value);

            result.Add(new AllocationAvailabilityResponse
            {
                EmployeeId = employee.EmployeeId,
                FullName = $"{employee.FirstName} {employee.LastName}",
                ProjectId = projectId,
                IsAssignedToProject = projectEmployeeIds.Contains(employee.EmployeeId),
                IsProjectManager = projectManagerIds.Contains(employee.EmployeeId),
                WorkNormHoursPerDay = workNormHours,
                WorkingDays = workingDays,
                CapacityHours = capacityHours,
                ExistingAllocatedHours = existingHours,
                AvailableHours = availableHours,
                MinimumDailyAvailableHours = minimumDailyAvailable,
                IsOnLeave = isOnLeave,
                MeetsSkillRequirement = skillMatch.MeetsRequirement,
                RequiredSkillId = requiredSkill?.SkillId,
                RequiredSkillName = requiredSkill?.SkillName,
                RequiredSkillLevel = requiredSkill?.SkillLevel,
                MatchedSkillId = skillMatch.MatchedSkill?.SkillId,
                MatchedSkillName = skillMatch.MatchedSkill?.SkillName,
                MatchedSkillLevel = skillMatch.MatchedSkill?.SkillLevel,
                CanTakeRequestedHours = canTakeRequestedHours,
                Status = !skillMatch.MeetsRequirement
                    ? "Skill requirement not met"
                    : isOnLeave
                    ? "On leave"
                    : canTakeRequestedHours
                        ? "Available"
                        : "Insufficient availability"
            });
        }

        return result
            .OrderByDescending(x => x.CanTakeRequestedHours)
            .ThenByDescending(x => x.MinimumDailyAvailableHours)
            .ThenByDescending(x => x.AvailableHours)
            .ThenBy(x => x.FullName)
            .ToList();
    }

    public async Task<TaskPlanningPreviewResponse> BuildTaskPlanAsync(TaskPlanningPreviewRequest request)
    {
        var startDate = request.PlannedStartDate.Date;
        var endDate = request.PlannedEndDate.Date;
        var workingDays = CountWorkingDays(startDate, endDate);
        var availability = await GetAvailabilityAsync(new AllocationAvailabilityRequest
        {
            ProjectId = request.ProjectId,
            SkillId = request.RequiredSkillId,
            StartDate = startDate,
            EndDate = endDate
        });

        var candidates = availability.Select(item => new TaskPlanningCandidateResponse
        {
            EmployeeId = item.EmployeeId,
            FullName = item.FullName,
            ProjectId = item.ProjectId,
            IsAssignedToProject = item.IsAssignedToProject,
            IsProjectManager = item.IsProjectManager,
            WorkNormHoursPerDay = item.WorkNormHoursPerDay,
            WorkingDays = item.WorkingDays,
            CapacityHours = item.CapacityHours,
            ExistingAllocatedHours = item.ExistingAllocatedHours,
            AvailableHours = item.AvailableHours,
            MinimumDailyAvailableHours = item.MinimumDailyAvailableHours,
            IsOnLeave = item.IsOnLeave,
            MeetsSkillRequirement = item.MeetsSkillRequirement,
            RequiredSkillId = item.RequiredSkillId,
            RequiredSkillName = item.RequiredSkillName,
            RequiredSkillLevel = item.RequiredSkillLevel,
            MatchedSkillId = item.MatchedSkillId,
            MatchedSkillName = item.MatchedSkillName,
            MatchedSkillLevel = item.MatchedSkillLevel,
            CanTakeRequestedHours = item.CanTakeRequestedHours,
            Status = item.Status,
            MaxAssignableHours = item.MeetsSkillRequirement && !item.IsOnLeave
                ? workingDays * Math.Min(8m, Math.Floor(item.MinimumDailyAvailableHours))
                : 0
        }).ToList();

        var eligible = candidates
            .Where(item => item.MaxAssignableHours > 0 && !request.ExcludedEmployeeIds.Contains(item.EmployeeId))
            .OrderByDescending(item => item.IsAssignedToProject)
            .ThenBy(item => item.ExistingAllocatedHours)
            .ThenByDescending(item => item.MinimumDailyAvailableHours)
            .ThenBy(item => item.FullName)
            .ToList();

        var remaining = Math.Max(request.EstimatedHours, 0);
        var automaticPlan = new List<PlannedAllocationResponse>();
        foreach (var candidate in eligible)
        {
            if (remaining <= 0.01m || workingDays == 0)
                break;

            var maximumDailyHours = (int)Math.Min(8m, Math.Floor(candidate.MinimumDailyAvailableHours));
            var bestHoursPerDay = 0;
            var bestWorkingDays = 0;
            var bestTotalHours = 0m;
            for (var hoursPerDay = 1; hoursPerDay <= maximumDailyHours; hoursPerDay++)
            {
                var assignableDays = Math.Min(workingDays, (int)Math.Floor(remaining / hoursPerDay));
                var totalHours = assignableDays * hoursPerDay;
                if (totalHours > bestTotalHours)
                {
                    bestHoursPerDay = hoursPerDay;
                    bestWorkingDays = assignableDays;
                    bestTotalHours = totalHours;
                }
            }

            if (bestHoursPerDay <= 0 || bestWorkingDays <= 0)
                continue;

            var allocationEndDate = GetEndDateForWorkingDays(startDate, bestWorkingDays);
            automaticPlan.Add(new PlannedAllocationResponse
            {
                EmployeeId = candidate.EmployeeId,
                EmployeeName = candidate.FullName,
                HoursPerDay = bestHoursPerDay,
                TotalHours = bestTotalHours,
                AllocationStartDate = startDate,
                AllocationEndDate = allocationEndDate
            });
            remaining = Math.Max(remaining - bestTotalHours, 0);
        }

        return new TaskPlanningPreviewResponse
        {
            PlannedStartDate = startDate,
            PlannedEndDate = endDate,
            WorkingDays = workingDays,
            EstimatedHours = request.EstimatedHours,
            SafeAvailableHours = eligible.Sum(item => item.MaxAssignableHours),
            RemainingUncoveredHours = remaining,
            CanFullyStaff = remaining <= 0.05m,
            Candidates = candidates
                .OrderByDescending(item => item.MaxAssignableHours > 0)
                .ThenByDescending(item => item.IsAssignedToProject)
                .ThenByDescending(item => item.MaxAssignableHours)
                .ThenBy(item => item.FullName)
                .ToList(),
            AutomaticPlan = automaticPlan
        };
    }

    public async Task<AllocationSimulationResponse> SimulateAllocationAsync(AllocationSimulationRequest request)
    {
        var startDate = request.StartDate.Date;
        var endDate = (request.EndDate ?? request.StartDate).Date;
        var task = await _context.TaskItems
            .AsNoTracking()
            .Include(t => t.RequiredSkill)
            .FirstOrDefaultAsync(t => t.ProjectId == request.ProjectId && t.TaskId == request.TaskId);

        if (task == null)
        {
            return new AllocationSimulationResponse
            {
                ProjectId = request.ProjectId,
                TaskId = request.TaskId,
                StartDate = startDate,
                EndDate = endDate,
                HoursPerDay = request.HoursPerDay,
                CanAllocate = false,
                Reasons = { "Task does not exist." }
            };
        }

        var workingDays = CountWorkingDays(startDate, endDate);
        var requestedTotalHours = workingDays * request.HoursPerDay;
        var currentTaskAllocatedHours = await GetCurrentTaskAllocatedHoursAsync(request.ProjectId, request.TaskId);
        var taskEstimatedHours = task.EstimatedHours ?? 0;
        var taskRemainingAfterSimulation = taskEstimatedHours - currentTaskAllocatedHours - requestedTotalHours;

        var effectiveSkillId = string.IsNullOrWhiteSpace(request.SkillId) ? task.RequiredSkillId : request.SkillId;

        var availability = await GetAvailabilityAsync(new AllocationAvailabilityRequest
        {
            ProjectId = request.ProjectId,
            EmployeeId = request.EmployeeId,
            SkillId = effectiveSkillId,
            StartDate = startDate,
            EndDate = endDate,
            RequiredHoursPerDay = request.HoursPerDay
        });

        var candidates = availability
            .Where(a => a.CanTakeRequestedHours)
            .Take(string.IsNullOrWhiteSpace(request.EmployeeId) ? 5 : 1)
            .ToList();

        var reasons = new List<string>();
        if (startDate > endDate)
            reasons.Add("EndDate cannot be before StartDate.");
        if (request.HoursPerDay <= 0)
            reasons.Add("HoursPerDay must be greater than zero.");
        if (workingDays == 0)
            reasons.Add("The selected interval has no working days.");
        if (taskEstimatedHours > 0 && taskRemainingAfterSimulation < 0)
            reasons.Add("The simulated allocation exceeds the task estimated hours.");
        if (!candidates.Any())
            reasons.Add(string.IsNullOrWhiteSpace(request.EmployeeId)
                ? "No available employee found for this interval and required skill."
                : "The selected employee is not available or does not meet the required skill.");

        return new AllocationSimulationResponse
        {
            ProjectId = request.ProjectId,
            TaskId = request.TaskId,
            TaskName = task.TaskName,
            StartDate = startDate,
            EndDate = endDate,
            HoursPerDay = request.HoursPerDay,
            RequestedTotalHours = requestedTotalHours,
            CurrentTaskAllocatedHours = currentTaskAllocatedHours,
            TaskEstimatedHours = taskEstimatedHours,
            TaskRemainingHoursAfterSimulation = taskRemainingAfterSimulation,
            RequiredSkillId = task.RequiredSkillId,
            RequiredSkillName = task.RequiredSkill?.SkillName,
            RequiredSkillLevel = task.RequiredSkill?.SkillLevel,
            CanAllocate = reasons.Count == 0,
            Reasons = reasons,
            Candidates = candidates
        };
    }

    public async Task<(bool Success, string? Error, AllocationResponse? Allocation)> CreateAllocationAsync(CreateAllocationRequest request)
    {
        var employee = await _context.Employees.Include(e => e.WorkNorm)
            .FirstOrDefaultAsync(e => e.EmployeeId == request.EmployeeId);
        if (employee?.WorkNorm == null) return (false, "Invalid employee or work norm.", null);
        var task = await _context.TaskItems.Include(t => t.Project)
            .Include(t => t.RequiredSkill)
            .FirstOrDefaultAsync(t => t.ProjectId == request.ProjectId && t.TaskId == request.TaskId);
        if (task == null) return (false, "Invalid task.", null);
        if (!await EmployeeMeetsSkillRequirementAsync(request.EmployeeId, task.RequiredSkillId))
            return (false, "Employee does not meet the task required skill level.", null);
        var endDate = request.AllocationEndDate ?? request.AllocationStartDate;
        if (request.AllocationStartDate.Date < DateTime.Today) return (false, "Allocation start date cannot be in the past.", null);
        if (request.AllocationStartDate.Date > endDate.Date) return (false, "Invalid interval.", null);
        if (request.AllocatedHours <= 0) return (false, "Invalid hours.", null);
        var duplicate = await _context.Allocations.FindAsync(request.EmployeeId, request.ProjectId, request.TaskId);
        if (duplicate != null) return (false, "Duplicate allocation.", null);
        var newTotalHours = CalculateTotalAllocationHours(request.AllocationStartDate, endDate, request.AllocatedHours);
        var existingAllocations = await _context.Allocations
            .Where(a => a.ProjectId == request.ProjectId && a.TaskId == request.TaskId)
            .ToListAsync();
        var currentTotal = 0m;
        foreach (var existingAllocation in existingAllocations)
        {
            currentTotal += await CalculateEffectiveAllocationHoursAsync(
                existingAllocation.EmployeeId,
                existingAllocation.AllocationStartDate,
                existingAllocation.AllocationEndDate ?? existingAllocation.AllocationStartDate,
                existingAllocation.AllocatedHours);
        }
        var estimatedHours = task.EstimatedHours ?? 0;
        if (estimatedHours > 0 && currentTotal + newTotalHours > estimatedHours)
            return (false, "Task hours exceeded.", null);
        for (var date = request.AllocationStartDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) continue;
            var dailyHours = await GetEmployeeAllocatedHoursForDateAsync(request.EmployeeId, date);
            if (dailyHours + request.AllocatedHours > employee.WorkNorm.WorkHours)
                return (false, "Work norm exceeded.", null);
        }
        var allocation = new Allocation
        {
            EmployeeId = request.EmployeeId,
            ProjectId = request.ProjectId,
            TaskId = request.TaskId,
            AllocationStartDate = request.AllocationStartDate.Date,
            AllocationEndDate = endDate.Date,
            AllocatedHours = request.AllocatedHours
        };
        _context.Allocations.Add(allocation);
        await _context.SaveChangesAsync();
        var response = (await GetByTaskAsync(request.ProjectId, request.TaskId))
            .First(a => a.EmployeeId == request.EmployeeId);
        response.TotalAllocationHours = newTotalHours;
        return (true, null, response);
    }

    private IQueryable<AllocationResponse> BuildAllocationQuery()
    {
        return _context.Allocations
            .Include(a => a.Employee)
            .Include(a => a.Project)
            .Include(a => a.TaskItem)
            .Select(a => new AllocationResponse
            {
                EmployeeId = a.EmployeeId,
                ProjectId = a.ProjectId,
                TaskId = a.TaskId,
                EmployeeName = a.Employee == null ? null : a.Employee.LastName + " " + a.Employee.FirstName,
                ProjectName = a.Project == null ? null : a.Project.ProjectName,
                TaskName = a.TaskItem == null ? null : a.TaskItem.TaskName,
                RequiredSkillId = a.TaskItem == null ? null : a.TaskItem.RequiredSkillId,
                RequiredSkillName = a.TaskItem == null || a.TaskItem.RequiredSkill == null ? null : a.TaskItem.RequiredSkill.SkillName,
                RequiredSkillLevel = a.TaskItem == null || a.TaskItem.RequiredSkill == null ? null : a.TaskItem.RequiredSkill.SkillLevel,
                AllocationStartDate = a.AllocationStartDate,
                AllocationEndDate = a.AllocationEndDate,
                AllocatedHours = a.AllocatedHours,
                TotalAllocationHours = 0
            });
    }

    private async Task<decimal> GetCurrentTaskAllocatedHoursAsync(string projectId, string taskId)
    {
        var allocations = await _context.Allocations
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId && a.TaskId == taskId)
            .ToListAsync();

        var total = 0m;
        foreach (var allocation in allocations)
        {
            total += await CalculateEffectiveAllocationHoursAsync(
                allocation.EmployeeId,
                allocation.AllocationStartDate,
                allocation.AllocationEndDate ?? allocation.AllocationStartDate,
                allocation.AllocatedHours);
        }
        return total;
    }

    private static DateTime GetEndDateForWorkingDays(DateTime startDate, int workingDays)
    {
        var date = startDate.Date;
        var counted = 0;
        while (counted < workingDays)
        {
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                counted++;
            if (counted < workingDays)
                date = date.AddDays(1);
        }
        return date;
    }

    private async Task<List<(DateTime Start, DateTime End)>> GetUncoveredLeavePeriodsAsync(string employeeId, DateTime startDate, DateTime endDate)
    {
        var result = new List<(DateTime Start, DateTime End)>();
        if (!_context.Database.IsRelational())
            return result;
        try
        {
            var connection = _context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = @"SELECT StartDate, EndDate FROM EmployeeLeave
WHERE EmployeeId = @employeeId AND ReplacementEmployeeId IS NULL
  AND StartDate <= @endDate AND EndDate >= @startDate";
            AddParameter(command, "@employeeId", employeeId);
            AddParameter(command, "@startDate", startDate.Date);
            AddParameter(command, "@endDate", endDate.Date);
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add((reader.GetDateTime(0).Date, reader.GetDateTime(1).Date));
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 208)
        {
            return result;
        }
        return result;
    }

    private async Task<decimal> GetReplacementCoverageHoursForDateAsync(string employeeId, DateTime date)
    {
        if (!_context.Database.IsRelational())
            return 0;
        try
        {
            var connection = _context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = @"SELECT COALESCE(SUM(a.HoursPerDay), 0)
FROM EmployeeLeave l
JOIN Allocation a ON a.EmployeeId = l.EmployeeId
WHERE l.ReplacementEmployeeId = @employeeId
  AND l.StartDate <= @date AND l.EndDate >= @date
  AND a.AllocationStartDate <= @date
  AND COALESCE(a.AllocationEndDate, a.AllocationStartDate) >= @date";
            AddParameter(command, "@employeeId", employeeId);
            AddParameter(command, "@date", date.Date);
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
            return Convert.ToDecimal(await command.ExecuteScalarAsync());
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 208)
        {
            return 0;
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
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

            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var value = await command.ExecuteScalarAsync();
            return Convert.ToInt32(value) > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EmployeeMeetsSkillRequirementAsync(string employeeId, string? requiredSkillId)
    {
        var requiredSkill = await GetRequiredSkillAsync(requiredSkillId);
        return (await GetEmployeeSkillMatchAsync(employeeId, requiredSkill)).MeetsRequirement;
    }

    private async Task<Skill?> GetRequiredSkillAsync(string? requiredSkillId)
    {
        if (string.IsNullOrWhiteSpace(requiredSkillId))
            return null;

        return await _context.Skills
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SkillId == requiredSkillId);
    }

    private async Task<SkillMatch> GetEmployeeSkillMatchAsync(string employeeId, Skill? requiredSkill)
    {
        if (requiredSkill == null)
            return new SkillMatch(true, null);

        var employeeSkills = await _context.EmployeeSkills
            .AsNoTracking()
            .Include(es => es.Skill)
            .Where(es => es.EmployeeId == employeeId && es.Skill != null)
            .Select(es => es.Skill!)
            .ToListAsync();

        var requiredRank = GetSkillLevelRank(requiredSkill.SkillLevel);
        var match = employeeSkills
            .Where(skill => string.Equals(skill.SkillName, requiredSkill.SkillName, StringComparison.OrdinalIgnoreCase))
            .Where(skill => GetSkillLevelRank(skill.SkillLevel) >= requiredRank)
            .OrderBy(skill => GetSkillLevelRank(skill.SkillLevel))
            .FirstOrDefault();

        return new SkillMatch(match != null, match);
    }

    private static int GetSkillLevelRank(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
            return 0;

        var normalized = level.Trim().ToLowerInvariant();
        if (normalized.Contains("junior") || normalized is "1" or "basic" or "beginner" or "incepator")
            return 1;
        if (normalized.Contains("mid") || normalized.Contains("medium") || normalized.Contains("mediu") || normalized is "2")
            return 2;
        if (normalized.Contains("senior") || normalized.Contains("expert") || normalized is "3" or "advanced" or "avansat")
            return 3;

        return 0;
    }

    private sealed record SkillMatch(bool MeetsRequirement, Skill? MatchedSkill);
}
