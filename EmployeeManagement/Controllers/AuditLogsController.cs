using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public class AuditLogsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAccessScopeService _accessScope;

    public AuditLogsController(AppDbContext context, IAccessScopeService accessScope)
    {
        _context = context;
        _accessScope = accessScope;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditLogResponse>>> Get([FromQuery] string? entityType, [FromQuery] string? projectId, [FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 500);
        IQueryable<Entities.AuditLog> query = _context.AuditLogs.AsNoTracking();
        if (User.IsInRole(RoleNames.Manager))
        {
            var managed = await _accessScope.GetManagedProjectIdsAsync(User);
            var teamEmployeeIds = _context.Allocations
                .AsNoTracking()
                .Where(allocation => managed.Contains(allocation.ProjectId))
                .Select(allocation => allocation.EmployeeId)
                .Distinct();

            query = query.Where(item =>
                item.ProjectId != null &&
                managed.Contains(item.ProjectId) &&
                item.ActorRole == RoleNames.Employee &&
                item.ActorEmployeeId != null &&
                teamEmployeeIds.Contains(item.ActorEmployeeId));
        }
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            if (!await _accessScope.CanViewProjectAsync(User, projectId)) return Forbid();
            query = query.Where(item => item.ProjectId == projectId);
        }
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(item => item.EntityType == entityType);

        return Ok(await query.OrderByDescending(item => item.CreatedAt).Take(limit).Select(item => new AuditLogResponse
        {
            AuditLogId = item.AuditLogId,
            CreatedAt = item.CreatedAt,
            ActorEmployeeId = item.ActorEmployeeId,
            ActorName = _context.Employees
                .Where(employee => employee.EmployeeId == item.ActorEmployeeId)
                .Select(employee => employee.FirstName + " " + employee.LastName)
                .FirstOrDefault(),
            ActorRole = item.ActorRole,
            Action = item.Action,
            EntityType = item.EntityType,
            EntityId = item.EntityId,
            ProjectId = item.ProjectId,
            Summary = item.Summary,
            BeforeJson = item.BeforeJson,
            AfterJson = item.AfterJson
        }).ToListAsync());
    }
}
