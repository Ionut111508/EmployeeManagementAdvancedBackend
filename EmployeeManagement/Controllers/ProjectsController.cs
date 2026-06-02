using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Data;
using EmployeeManagement.Entities;
using EmployeeManagement.DTOs;
using EmployeeManagement.Services;

namespace EmployeeManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IUserRoleService _userRoleService;

        public ProjectsController(AppDbContext context, IUserRoleService userRoleService)
        {
            _context = context;
            _userRoleService = userRoleService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectDto>>> GetAll()
        {
            var projects = await _context.Projects.ToListAsync();
            var dtos = projects.Select(p => new ProjectDto { ProjectId = p.ProjectId, ProjectName = p.ProjectName }).ToList();
            return Ok(dtos);
        }

        [HttpGet("visible-to/{viewerEmployeeId}")]
        public async Task<ActionResult<IEnumerable<ProjectDto>>> GetVisibleTo(string viewerEmployeeId)
        {
            var access = await _userRoleService.GetAccessForEmployeeAsync(viewerEmployeeId);
            if (access == null)
                return NotFound("Viewer employee was not found.");

            IQueryable<Project> query = _context.Projects.AsNoTracking();

            if (access.Role == RoleNames.Employee)
            {
                query = query.Where(p => _context.Allocations.Any(a =>
                    a.ProjectId == p.ProjectId && a.EmployeeId == viewerEmployeeId));
            }
            else if (access.Role == RoleNames.Manager)
            {
                var managedProjectIds = access.ManagedProjectIds;
                query = query.Where(p => managedProjectIds.Contains(p.ProjectId));
            }

            var projects = await query
                .OrderBy(p => p.ProjectName)
                .Select(p => new ProjectDto
                {
                    ProjectId = p.ProjectId,
                    ProjectName = p.ProjectName
                })
                .ToListAsync();

            return Ok(projects);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectDto>> GetById(string id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
                return NotFound();

            return Ok(new ProjectDto { ProjectId = project.ProjectId, ProjectName = project.ProjectName });
        }

        [HttpPost]
        public async Task<ActionResult<ProjectDto>> Create(ProjectCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ProjectId) || string.IsNullOrWhiteSpace(dto.ProjectName))
                return BadRequest("ProjectId and ProjectName are required");

            var project = new Project
            {
                ProjectId = dto.ProjectId,
                ProjectName = dto.ProjectName
            };

            _context.Projects.Add(project);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (await _context.Projects.AnyAsync(p => p.ProjectId == dto.ProjectId))
                    return BadRequest("Project with this ID already exists");
                throw;
            }

            var resultDto = new ProjectDto { ProjectId = project.ProjectId, ProjectName = project.ProjectName };
            return CreatedAtAction(nameof(GetById), new { id = project.ProjectId }, resultDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, ProjectUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ProjectName))
                return BadRequest("ProjectName is required");

            var project = await _context.Projects.FindAsync(id);
            if (project == null)
                return NotFound();

            project.ProjectName = dto.ProjectName;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
                return NotFound();

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
