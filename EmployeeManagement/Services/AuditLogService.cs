using System.Security.Claims;
using System.Text.Json;
using EmployeeManagement.Data;
using EmployeeManagement.Entities;

namespace EmployeeManagement.Services;

public interface IAuditLogService
{
    Task RecordAsync(ClaimsPrincipal user, string action, string entityType, string entityId, string summary, string? projectId = null, object? before = null, object? after = null);
}

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _context;

    public AuditLogService(AppDbContext context) => _context = context;

    public async Task RecordAsync(ClaimsPrincipal user, string action, string entityType, string entityId, string summary, string? projectId = null, object? before = null, object? after = null)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            CreatedAt = DateTime.UtcNow,
            ActorEmployeeId = user.FindFirst("employee_id")?.Value,
            ActorRole = user.FindFirst(ClaimTypes.Role)?.Value ?? RoleNames.Employee,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ProjectId = projectId,
            Summary = summary,
            BeforeJson = before == null ? null : JsonSerializer.Serialize(before),
            AfterJson = after == null ? null : JsonSerializer.Serialize(after)
        });
        await _context.SaveChangesAsync();
    }
}
