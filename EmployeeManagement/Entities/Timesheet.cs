namespace EmployeeManagement.Entities
{
    public class Timesheet
    {
        public string ProjectId { get; set; } = null!;
        public string TaskId { get; set; } = null!;
        public string EmployeeId { get; set; } = null!;
        public DateTime WorkDate { get; set; }
        public decimal WorkedHours { get; set; }
        public string Status { get; set; } = Services.TimesheetStatuses.Pending;
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedByEmployeeId { get; set; }
        public string? ReviewComment { get; set; }

        public Project? Project { get; set; }
        public TaskItem? TaskItem { get; set; }
        public Employee? Employee { get; set; }
    }
}
