namespace EmployeeManagement.Entities
{
    public class TaskItem
    {
        public string ProjectId { get; set; } = null!;
        public string TaskId { get; set; } = null!;
        public string TaskName { get; set; } = null!;
        public decimal? EstimatedHours { get; set; }
        public string DescriptionId { get; set; } = null!;
        public string? RequiredSkillId { get; set; }

        public Project? Project { get; set; }
        public TaskDescription? Description { get; set; }
        public Skill? RequiredSkill { get; set; }
        public ICollection<TaskComment> TaskComments { get; set; } = new List<TaskComment>();
        public ICollection<Allocation> Allocations { get; set; } = new List<Allocation>();
        public ICollection<Timesheet> Timesheets { get; set; } = new List<Timesheet>();
        public ICollection<TaskPeriod> TaskPeriods { get; set; } = new List<TaskPeriod>();
    }
}
