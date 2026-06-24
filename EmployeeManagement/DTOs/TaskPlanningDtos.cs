namespace EmployeeManagement.DTOs;

public class TaskPlanningPreviewRequest
{
    public string ProjectId { get; set; } = null!;
    public decimal EstimatedHours { get; set; }
    public string? RequiredSkillId { get; set; }
    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public List<string> ExcludedEmployeeIds { get; set; } = new();
}

public class TaskPlanningCandidateResponse : AllocationAvailabilityResponse
{
    public decimal MaxAssignableHours { get; set; }
}

public class PlannedAllocationResponse
{
    public string EmployeeId { get; set; } = null!;
    public string EmployeeName { get; set; } = null!;
    public decimal HoursPerDay { get; set; }
    public decimal TotalHours { get; set; }
    public DateTime AllocationStartDate { get; set; }
    public DateTime AllocationEndDate { get; set; }
}

public class TaskPlanningPreviewResponse
{
    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public int WorkingDays { get; set; }
    public decimal EstimatedHours { get; set; }
    public decimal SafeAvailableHours { get; set; }
    public decimal RemainingUncoveredHours { get; set; }
    public bool CanFullyStaff { get; set; }
    public List<TaskPlanningCandidateResponse> Candidates { get; set; } = new();
    public List<PlannedAllocationResponse> AutomaticPlan { get; set; } = new();
}

public class ManualTaskAllocationRequest
{
    public string EmployeeId { get; set; } = null!;
    public decimal HoursPerDay { get; set; }
    public DateTime? AllocationStartDate { get; set; }
    public DateTime? AllocationEndDate { get; set; }
}

public class CreatePlannedTaskRequest
{
    public string ProjectId { get; set; } = null!;
    public string TaskName { get; set; } = null!;
    public decimal EstimatedHours { get; set; }
    public string DescriptionText { get; set; } = null!;
    public string? RequiredSkillId { get; set; }
    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public string AllocationMode { get; set; } = "Automatic";
    public List<ManualTaskAllocationRequest> ManualAllocations { get; set; } = new();
}

public class CreatePlannedTaskResponse
{
    public TaskItemDto Task { get; set; } = null!;
    public List<AllocationResponse> Allocations { get; set; } = new();
    public decimal AllocatedHours { get; set; }
    public decimal RemainingHours { get; set; }
    public string StaffingStatus { get; set; } = null!;
}

public class ResourcePlanningOverviewResponse
{
    public DateTime CurrentStartDate { get; set; }
    public DateTime CurrentEndDate { get; set; }
    public DateTime FutureStartDate { get; set; }
    public DateTime FutureEndDate { get; set; }
    public List<AllocationAvailabilityResponse> IdleEmployees { get; set; } = new();
    public List<AllocationAvailabilityResponse> UnderutilizedEmployees { get; set; } = new();
    public List<AllocationAvailabilityResponse> BecomingAvailableEmployees { get; set; } = new();
}

public class AutoAllocationResponse
{
    public List<AllocationResponse> Allocations { get; set; } = new();
    public decimal AllocatedHours { get; set; }
    public decimal RemainingHours { get; set; }
    public string Status { get; set; } = null!;
}
