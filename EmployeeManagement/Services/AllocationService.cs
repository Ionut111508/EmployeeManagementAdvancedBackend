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

    public bool DatesOverlap(DateTime start1, DateTime end1, DateTime start2, DateTime end2)
    {
        return start1.Date <= end2.Date && start2.Date <= end1.Date;
    }

    public async Task<decimal> GetEmployeeAllocatedHoursForDateAsync(string employeeId, DateTime date)
    {
        return await _context.Allocations
            .Where(a => a.EmployeeId == employeeId &&
                        a.AllocationStartDate.Date <= date.Date &&
                        (a.AllocationEndDate ?? a.AllocationStartDate).Date >= date.Date)
            .SumAsync(a => a.AllocatedHours);
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

        if (!string.IsNullOrWhiteSpace(request.SkillId))
        {
            employeesQuery = employeesQuery.Where(e =>
                _context.EmployeeSkills.Any(s => s.EmployeeId == e.EmployeeId && s.SkillId == request.SkillId));
        }

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
            var canTakeRequestedHours = !isOnLeave &&
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
                CanTakeRequestedHours = canTakeRequestedHours,
                Status = isOnLeave
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

    public async Task<AllocationSimulationResponse> SimulateAllocationAsync(AllocationSimulationRequest request)
    {
        var startDate = request.StartDate.Date;
        var endDate = (request.EndDate ?? request.StartDate).Date;
        var task = await _context.TaskItems
            .AsNoTracking()
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

        var availability = await GetAvailabilityAsync(new AllocationAvailabilityRequest
        {
            ProjectId = request.ProjectId,
            EmployeeId = request.EmployeeId,
            SkillId = request.SkillId,
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
                ? "No available employee found for this interval."
                : "The selected employee is not available for this interval.");

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
            .FirstOrDefaultAsync(t => t.ProjectId == request.ProjectId && t.TaskId == request.TaskId);
        if (task == null) return (false, "Invalid task.", null);
        var endDate = request.AllocationEndDate ?? request.AllocationStartDate;
        if (request.AllocationStartDate.Date > endDate.Date) return (false, "Invalid interval.", null);
        if (request.AllocatedHours <= 0) return (false, "Invalid hours.", null);
        var duplicate = await _context.Allocations.FindAsync(request.EmployeeId, request.ProjectId, request.TaskId);
        if (duplicate != null) return (false, "Duplicate allocation.", null);
        var newTotalHours = CalculateTotalAllocationHours(request.AllocationStartDate, endDate, request.AllocatedHours);
        var existingAllocations = await _context.Allocations
            .Where(a => a.ProjectId == request.ProjectId && a.TaskId == request.TaskId)
            .ToListAsync();
        var currentTotal = existingAllocations.Sum(a =>
            CalculateTotalAllocationHours(a.AllocationStartDate, a.AllocationEndDate ?? a.AllocationStartDate, a.AllocatedHours));
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

        return allocations.Sum(a => CalculateTotalAllocationHours(
            a.AllocationStartDate,
            a.AllocationEndDate ?? a.AllocationStartDate,
            a.AllocatedHours));
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
}
