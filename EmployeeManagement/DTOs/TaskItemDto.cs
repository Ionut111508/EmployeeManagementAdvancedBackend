namespace EmployeeManagement.DTOs
{
    public class TaskItemDto
    {
        public string ProjectId { get; set; } = null!;
        public string TaskId { get; set; } = null!;
        public string TaskName { get; set; } = null!;
        public decimal? EstimatedHours { get; set; }
        public string? DescriptionId { get; set; }
        public string? RequiredSkillId { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public string Status { get; set; } = Services.TaskStatuses.Backlog;
        public string WorkflowStatus { get; set; } = Services.TaskStatuses.Backlog;
        public decimal ApprovedWorkedHours { get; set; }
        public decimal RemainingHours { get; set; }
        public ProjectDto? Project { get; set; }
        public TaskDescriptionDto? Description { get; set; }
        public SkillDto? RequiredSkill { get; set; }
    }

    public class TaskItemCreateDto
    {
        public string ProjectId { get; set; } = null!;
        public string TaskId { get; set; } = null!;
        public string TaskName { get; set; } = null!;
        public decimal? EstimatedHours { get; set; }
        public string? DescriptionId { get; set; }
        public string? RequiredSkillId { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
    }

    public class TaskItemUpdateDto
    {
        public string TaskName { get; set; } = null!;
        public decimal? EstimatedHours { get; set; }
        public string? DescriptionId { get; set; }
        public string? RequiredSkillId { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
    }
}
