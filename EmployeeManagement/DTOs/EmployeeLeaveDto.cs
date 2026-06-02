namespace EmployeeManagement.DTOs
{
    public class EmployeeLeaveDto
    {
        public string EmployeeLeaveId { get; set; } = null!;
        public string EmployeeId { get; set; } = null!;
        public string EmployeeName { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string LeaveType { get; set; } = null!;
        public string? Reason { get; set; }
        public string? ReplacementEmployeeId { get; set; }
        public string? ReplacementEmployeeName { get; set; }
    }

    public class EmployeeLeaveCreateDto
    {
        public string? EmployeeLeaveId { get; set; }
        public string EmployeeId { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string LeaveType { get; set; } = "Vacation";
        public string? Reason { get; set; }
        public string? ReplacementEmployeeId { get; set; }
    }

    public class EmployeeLeavePlanDto
    {
        public EmployeeLeaveDto Leave { get; set; } = null!;
        public bool HasDelayRisk { get; set; }
        public string Recommendation { get; set; } = null!;
        public List<EmployeeLeaveImpactDto> Impacts { get; set; } = new();
    }

    public class EmployeeLeaveImpactDto
    {
        public string ProjectId { get; set; } = null!;
        public string ProjectName { get; set; } = null!;
        public string TaskId { get; set; } = null!;
        public string TaskName { get; set; } = null!;
        public DateTime AllocationStartDate { get; set; }
        public DateTime? AllocationEndDate { get; set; }
        public DateTime OverlapStartDate { get; set; }
        public DateTime OverlapEndDate { get; set; }
        public decimal AllocatedHours { get; set; }
        public string? RequiredSkillId { get; set; }
        public string? RequiredSkillName { get; set; }
        public string? RequiredSkillLevel { get; set; }
        public string Status { get; set; } = null!;
        public List<AllocationAvailabilityResponse> ReplacementCandidates { get; set; } = new();
    }
}
