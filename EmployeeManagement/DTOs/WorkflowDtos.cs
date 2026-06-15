namespace EmployeeManagement.DTOs;

public class TimesheetReviewRequest
{
    public string Status { get; set; } = null!;
    public string? Comment { get; set; }
}

public class TimesheetResponse
{
    public string ProjectId { get; set; } = null!;
    public string TaskId { get; set; } = null!;
    public string EmployeeId { get; set; } = null!;
    public DateTime WorkDate { get; set; }
    public decimal WorkedHours { get; set; }
    public string Status { get; set; } = null!;
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByEmployeeId { get; set; }
    public string? ReviewComment { get; set; }
}

public class TaskStatusUpdateRequest
{
    public string Status { get; set; } = null!;
    public string? Comment { get; set; }
}

public class AuditLogResponse
{
    public long AuditLogId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ActorEmployeeId { get; set; }
    public string ActorRole { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string? ProjectId { get; set; }
    public string Summary { get; set; } = null!;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}

public class NotificationResponse
{
    public string NotificationId { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? ProjectId { get; set; }
    public string? TaskId { get; set; }
    public string? EmployeeId { get; set; }
    public DateTime? RelevantDate { get; set; }
}
