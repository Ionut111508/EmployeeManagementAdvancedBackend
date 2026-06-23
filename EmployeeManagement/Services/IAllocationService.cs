using EmployeeManagement.DTOs;

namespace EmployeeManagement.Services;

public interface IAllocationService
{
    int CountWorkingDays(DateTime start, DateTime end);
    decimal CalculateTotalAllocationHours(DateTime start, DateTime end, decimal hoursPerDay);
    Task<decimal> CalculateEffectiveAllocationHoursAsync(string employeeId, DateTime start, DateTime end, decimal hoursPerDay);
    bool DatesOverlap(DateTime start1, DateTime end1, DateTime start2, DateTime end2);
    Task<decimal> GetEmployeeAllocatedHoursForDateAsync(string employeeId, DateTime date);
    Task<(bool Success, string? Error, AllocationResponse? Allocation)> CreateAllocationAsync(CreateAllocationRequest request);
    Task<List<AllocationResponse>> GetAllAsync();
    Task<List<AllocationResponse>> GetByEmployeeAsync(string employeeId);
    Task<List<AllocationResponse>> GetByProjectAsync(string projectId);
    Task<List<AllocationResponse>> GetByTaskAsync(string projectId, string taskId);
    Task<List<AllocationAvailabilityResponse>> GetAvailabilityAsync(AllocationAvailabilityRequest request);
    Task<TaskPlanningPreviewResponse> BuildTaskPlanAsync(TaskPlanningPreviewRequest request);
    Task<AllocationSimulationResponse> SimulateAllocationAsync(AllocationSimulationRequest request);
    Task<bool> EmployeeMeetsSkillRequirementAsync(string employeeId, string? requiredSkillId);
}
