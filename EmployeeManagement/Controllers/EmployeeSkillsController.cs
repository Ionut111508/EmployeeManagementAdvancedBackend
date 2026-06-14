using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Authorization;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeeSkillsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAccessScopeService _accessScope;

    public EmployeeSkillsController(AppDbContext context, IAccessScopeService accessScope)
    {
        _context = context;
        _accessScope = accessScope;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        IQueryable<EmployeeSkill> query = _context.EmployeeSkills.AsNoTracking();
        if (User.IsInRole(RoleNames.Employee))
        {
            var employeeId = _accessScope.GetCurrentEmployeeId(User);
            query = query.Where(x => x.EmployeeId == employeeId);
        }
        else if (User.IsInRole(RoleNames.Manager))
        {
            var projectIds = await _accessScope.GetManagedProjectIdsAsync(User);
            query = query.Where(x => _context.Allocations.Any(a => a.EmployeeId == x.EmployeeId && projectIds.Contains(a.ProjectId)) ||
                _context.ProjectManagers.Any(pm => pm.EmployeeId == x.EmployeeId && projectIds.Contains(pm.ProjectId)));
        }

        var result = await query
            .Select(x => new EmployeeSkillResponse
            {
                EmployeeId = x.EmployeeId,
                SkillId = x.SkillId,
                AcquiredDate = x.AcquiredDate,
                Employee = x.Employee != null ? new EmployeeBasicDto
                {
                    EmployeeId = x.Employee.EmployeeId,
                    FirstName = x.Employee.FirstName,
                    LastName = x.Employee.LastName,
                    Email = x.Employee.Email,
                    PhoneNumber = x.Employee.PhoneNumber,
                    AccountId = x.Employee.AccountId,
                    WorkNormId = x.Employee.WorkNormId
                } : null,
                Skill = x.Skill != null ? new SkillBasicDto
                {
                    SkillId = x.Skill.SkillId,
                    SkillName = x.Skill.SkillName,
                    SkillLevel = x.Skill.SkillLevel
                } : null
            })
            .ToListAsync();
        return Ok(result);
    }

    [HttpGet("employee/{employeeId}")]
    public async Task<IActionResult> GetByEmployee(string employeeId)
    {
        if (!await _accessScope.CanViewEmployeeAsync(User, employeeId))
            return Forbid();

        if (!await _context.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId))
            return NotFound("Employee not found.");

        var result = await _context.EmployeeSkills
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .Select(x => new EmployeeSkillByEmployeeResponse
            {
                EmployeeId = x.EmployeeId,
                SkillId = x.SkillId,
                AcquiredDate = x.AcquiredDate,
                Skill = x.Skill != null ? new SkillBasicDto
                {
                    SkillId = x.Skill.SkillId,
                    SkillName = x.Skill.SkillName,
                    SkillLevel = x.Skill.SkillLevel
                } : null
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("skill/{skillId}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> GetBySkill(string skillId)
    {
        if (!await _context.Skills.AsNoTracking().AnyAsync(x => x.SkillId == skillId))
            return NotFound("Skill not found.");

        var result = await _context.EmployeeSkills
            .AsNoTracking()
            .Where(x => x.SkillId == skillId)
            .Select(x => new EmployeeSkillBySkillResponse
            {
                EmployeeId = x.EmployeeId,
                SkillId = x.SkillId,
                AcquiredDate = x.AcquiredDate,
                Employee = x.Employee != null ? new EmployeeBasicDto
                {
                    EmployeeId = x.Employee.EmployeeId,
                    FirstName = x.Employee.FirstName,
                    LastName = x.Employee.LastName,
                    Email = x.Employee.Email,
                    PhoneNumber = x.Employee.PhoneNumber,
                    AccountId = x.Employee.AccountId,
                    WorkNormId = x.Employee.WorkNormId
                } : null,
                Skill = x.Skill != null ? new SkillBasicDto
                {
                    SkillId = x.Skill.SkillId,
                    SkillName = x.Skill.SkillName,
                    SkillLevel = x.Skill.SkillLevel
                } : null
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Create(EmployeeSkillRequest request)
    {
        if (!await _accessScope.CanManageEmployeeAsync(User, request.EmployeeId))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.EmployeeId) || string.IsNullOrWhiteSpace(request.SkillId))
            return BadRequest("EmployeeId and SkillId are required.");

        if (!await _context.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == request.EmployeeId))
            return BadRequest("Selected employee does not exist.");

        if (!await _context.Skills.AsNoTracking().AnyAsync(x => x.SkillId == request.SkillId))
            return BadRequest("Selected skill does not exist.");

        var exists = await _context.EmployeeSkills.AsNoTracking().AnyAsync(x => x.EmployeeId == request.EmployeeId && x.SkillId == request.SkillId);
        if (exists)
            return BadRequest("Employee already has this skill.");

        var item = new EmployeeSkill
        {
            EmployeeId = request.EmployeeId,
            SkillId = request.SkillId,
            AcquiredDate = request.AcquiredDate ?? DateTime.Today
        };

        _context.EmployeeSkills.Add(item);
        await _context.SaveChangesAsync();

        var response = new EmployeeSkillResponse
        {
            EmployeeId = item.EmployeeId,
            SkillId = item.SkillId,
            AcquiredDate = item.AcquiredDate
        };

        return CreatedAtAction(nameof(GetByEmployee), new { employeeId = item.EmployeeId }, response);
    }

    [HttpDelete("{employeeId}/{skillId}")]
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Delete(string employeeId, string skillId)
    {
        if (!await _accessScope.CanManageEmployeeAsync(User, employeeId))
            return Forbid();

        var item = await _context.EmployeeSkills.FindAsync(employeeId, skillId);
        if (item == null)
            return NotFound("Employee skill assignment not found.");

        _context.EmployeeSkills.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
