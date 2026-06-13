namespace EmployeeManagement.DTOs;

public class CreateAllocationRequest
{
    public string EmployeeId { get; set; } = null!;
    public string ProjectId { get; set; } = null!;
    public string TaskId { get; set; } = null!;
    public DateTime AllocationStartDate { get; set; }
    public DateTime? AllocationEndDate { get; set; }
    public decimal AllocatedHours { get; set; }
}

public class AllocationResponse
{
    public string EmployeeId { get; set; } = null!;
    public string ProjectId { get; set; } = null!;
    public string TaskId { get; set; } = null!;
    public string? EmployeeName { get; set; }
    public string? ProjectName { get; set; }
    public string? TaskName { get; set; }
    public string? RequiredSkillId { get; set; }
    public string? RequiredSkillName { get; set; }
    public string? RequiredSkillLevel { get; set; }
    public DateTime? AllocationStartDate { get; set; }
    public DateTime? AllocationEndDate { get; set; }
    public decimal AllocatedHours { get; set; }
    public decimal TotalAllocationHours { get; set; }
}

public class AllocationAvailabilityRequest
{
    public string? ProjectId { get; set; }
    public string? EmployeeId { get; set; }
    public string? SkillId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? RequiredHoursPerDay { get; set; }
    public bool OnlyProjectEmployees { get; set; }
}

public class AllocationAvailabilityResponse
{
    public string EmployeeId { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? ProjectId { get; set; }
    public bool IsAssignedToProject { get; set; }
    public bool IsProjectManager { get; set; }
    public decimal WorkNormHoursPerDay { get; set; }
    public int WorkingDays { get; set; }
    public decimal CapacityHours { get; set; }
    public decimal ExistingAllocatedHours { get; set; }
    public decimal AvailableHours { get; set; }
    public decimal MinimumDailyAvailableHours { get; set; }
    public bool IsOnLeave { get; set; }
    public bool MeetsSkillRequirement { get; set; } = true;
    public string? RequiredSkillId { get; set; }
    public string? RequiredSkillName { get; set; }
    public string? RequiredSkillLevel { get; set; }
    public string? MatchedSkillId { get; set; }
    public string? MatchedSkillName { get; set; }
    public string? MatchedSkillLevel { get; set; }
    public bool CanTakeRequestedHours { get; set; }
    public string Status { get; set; } = null!;
}

public class AllocationSimulationRequest
{
    public string? EmployeeId { get; set; }
    public string ProjectId { get; set; } = null!;
    public string TaskId { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal HoursPerDay { get; set; }
    public string? SkillId { get; set; }
}

public class AllocationSimulationResponse
{
    public string ProjectId { get; set; } = null!;
    public string TaskId { get; set; } = null!;
    public string TaskName { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal HoursPerDay { get; set; }
    public decimal RequestedTotalHours { get; set; }
    public decimal CurrentTaskAllocatedHours { get; set; }
    public decimal TaskEstimatedHours { get; set; }
    public decimal TaskRemainingHoursAfterSimulation { get; set; }
    public string? RequiredSkillId { get; set; }
    public string? RequiredSkillName { get; set; }
    public string? RequiredSkillLevel { get; set; }
    public bool CanAllocate { get; set; }
    public List<string> Reasons { get; set; } = new();
    public List<AllocationAvailabilityResponse> Candidates { get; set; } = new();
}

public class TaskStaffingResponse
{
    public string ProjectId { get; set; } = null!;
    public string ProjectName { get; set; } = null!;
    public string TaskId { get; set; } = null!;
    public string TaskName { get; set; } = null!;
    public decimal EstimatedHours { get; set; }
    public decimal AllocatedHours { get; set; }
    public decimal RemainingHours { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public int AllocatedPeople { get; set; }
    public string? RequiredSkillId { get; set; }
    public string? RequiredSkillName { get; set; }
    public string? RequiredSkillLevel { get; set; }
    public string Status { get; set; } = null!;
    public List<AllocationAvailabilityResponse> Candidates { get; set; } = new();
}

public class TimesheetRequest
{
    public string ProjectId { get; set; } = null!;
    public string TaskId { get; set; } = null!;
    public string EmployeeId { get; set; } = null!;
    public DateTime WorkDate { get; set; }
    public decimal WorkedHours { get; set; }
}

public class TaskCommentRequest
{
    public string TaskCommentId { get; set; } = null!;
    public string CommentText { get; set; } = null!;
    public DateTime? CommentDate { get; set; }
    public string ProjectId { get; set; } = null!;
    public string TaskId { get; set; } = null!;
    public string? EmployeeId { get; set; }
}

public class UpdateTaskCommentRequest
{
    public string CommentText { get; set; } = null!;
}

public class EmployeeSkillRequest
{
    public string EmployeeId { get; set; } = null!;
    public string SkillId { get; set; } = null!;
    public DateTime? AcquiredDate { get; set; }
}

public class EmployeeDepartmentRequest
{
    public string EmployeeId { get; set; } = null!;
    public string DepartmentId { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class ProjectManagerRequest
{
    public string EmployeeId { get; set; } = null!;
    public string ProjectId { get; set; } = null!;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class EmployeeWorkloadResponse
{
    public string EmployeeId { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public decimal WorkNormHours { get; set; }
    public List<AllocationResponse> PastAllocations { get; set; } = new();
    public List<AllocationResponse> CurrentAllocations { get; set; } = new();
    public List<AllocationResponse> FutureAllocations { get; set; } = new();
    public decimal TotalAllocatedHours { get; set; }
    public decimal TotalWorkedHours { get; set; }
    public int NumberOfProjects { get; set; }
    public int NumberOfTasks { get; set; }
}

public class TaskProgressResponse
{
    public string ProjectId { get; set; } = null!;
    public string TaskId { get; set; } = null!;
    public string TaskName { get; set; } = null!;
    public decimal EstimatedHours { get; set; }
    public decimal AllocatedHours { get; set; }
    public decimal WorkedHours { get; set; }
    public decimal AllocationProgressPercentage { get; set; }
    public decimal WorkedProgressPercentage { get; set; }
}

public class ProjectSummaryResponse
{
    public string ProjectId { get; set; } = null!;
    public string ProjectName { get; set; } = null!;
    public int NumberOfTasks { get; set; }
    public decimal TotalEstimatedHours { get; set; }
    public decimal TotalAllocatedHours { get; set; }
    public decimal TotalWorkedHours { get; set; }
    public decimal ProgressPercentage { get; set; }
}
