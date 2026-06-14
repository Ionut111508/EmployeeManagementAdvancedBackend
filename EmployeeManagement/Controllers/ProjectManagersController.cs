using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using EmployeeManagement.Services;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.Admin)]
public class ProjectManagersController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProjectManagersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("project/{projectId}")]
    public async Task<IActionResult> GetByProject(string projectId)
    {
        if (!await _context.Projects.AnyAsync(x => x.ProjectId == projectId))
            return NotFound("Project was not found.");

        var result = await _context.ProjectManagers
            .Where(x => x.ProjectId == projectId)
            .Include(x => x.Employee)
            .ToListAsync();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ProjectManagerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeId) || string.IsNullOrWhiteSpace(request.ProjectId))
            return BadRequest("EmployeeId and ProjectId are required.");

        if (request.EndDate.HasValue && request.StartDate.HasValue && request.EndDate.Value < request.StartDate.Value)
            return BadRequest("EndDate cannot be before StartDate.");

        var employee = await _context.Employees.Include(x => x.Account).FirstOrDefaultAsync(x => x.EmployeeId == request.EmployeeId);
        if (employee == null)
            return BadRequest("Selected employee does not exist.");

        if (!await _context.Projects.AnyAsync(x => x.ProjectId == request.ProjectId))
            return BadRequest("Selected project does not exist.");

        var exists = await _context.ProjectManagers.FindAsync(request.EmployeeId, request.ProjectId);
        if (exists != null)
            return BadRequest("Employee is already manager for this project.");

        var item = new ProjectManager
        {
            EmployeeId = request.EmployeeId,
            ProjectId = request.ProjectId,
            StartDate = request.StartDate ?? DateTime.Today,
            EndDate = request.EndDate
        };

        _context.ProjectManagers.Add(item);
        if (employee.Account != null)
            employee.Account.Role = RoleNames.Manager;
        await _context.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{employeeId}/{projectId}")]
    public async Task<IActionResult> Delete(string employeeId, string projectId)
    {
        var item = await _context.ProjectManagers.FindAsync(employeeId, projectId);
        if (item == null) return NotFound("Project manager assignment was not found.");

        _context.ProjectManagers.Remove(item);
        var hasOtherProjects = await _context.ProjectManagers.AnyAsync(pm =>
            pm.EmployeeId == employeeId && pm.ProjectId != projectId);
        if (!hasOtherProjects)
        {
            var account = await _context.Employees
                .Where(employee => employee.EmployeeId == employeeId)
                .Select(employee => employee.Account)
                .FirstOrDefaultAsync();
            if (account != null && RoleNames.Normalize(account.Role) == RoleNames.Manager)
                account.Role = RoleNames.Employee;
        }
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
