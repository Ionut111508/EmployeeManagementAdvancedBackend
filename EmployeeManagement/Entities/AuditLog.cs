namespace EmployeeManagement.Entities;

public class AuditLog
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
