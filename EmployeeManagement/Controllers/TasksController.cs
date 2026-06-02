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
    public class TasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IUserRoleService _userRoleService;

        public TasksController(AppDbContext context, IUserRoleService userRoleService)
        {
            _context = context;
            _userRoleService = userRoleService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskItemDto>>> GetAll()
        {
            var tasks = await _context.TaskItems
                .Include(t => t.Project)
                .Include(t => t.Description)
                .ToListAsync();

            var dtos = tasks.Select(t => new TaskItemDto
            {
                ProjectId = t.ProjectId,
                TaskId = t.TaskId,
                TaskName = t.TaskName,
                EstimatedHours = t.EstimatedHours,
                DescriptionId = t.DescriptionId,
                Project = t.Project != null ? new ProjectDto { ProjectId = t.Project.ProjectId, ProjectName = t.Project.ProjectName } : null,
                Description = t.Description != null ? new TaskDescriptionDto { DescriptionId = t.Description.DescriptionId, TaskDescriptionText = t.Description.TaskDescriptionText } : null
            }).ToList();

            return Ok(dtos);
        }

        [HttpGet("visible-to/{viewerEmployeeId}")]
        public async Task<ActionResult<IEnumerable<TaskItemDto>>> GetVisibleTo(string viewerEmployeeId)
        {
            var access = await _userRoleService.GetAccessForEmployeeAsync(viewerEmployeeId);
            if (access == null)
                return NotFound("Viewer employee was not found.");

            IQueryable<TaskItem> query = _context.TaskItems
                .AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.Description);

            if (access.Role == RoleNames.Employee)
            {
                query = query.Where(t => _context.Allocations.Any(a =>
                    a.ProjectId == t.ProjectId &&
                    a.TaskId == t.TaskId &&
                    a.EmployeeId == viewerEmployeeId));
            }
            else if (access.Role == RoleNames.Manager)
            {
                var managedProjectIds = access.ManagedProjectIds;
                query = query.Where(t => managedProjectIds.Contains(t.ProjectId));
            }

            var tasks = await query
                .OrderBy(t => t.ProjectId)
                .ThenBy(t => t.TaskName)
                .ToListAsync();

            return Ok(tasks.Select(ToDto).ToList());
        }

        [HttpGet("{projectId}/{taskId}")]
        public async Task<ActionResult<TaskItemDto>> GetById(string projectId, string taskId)
        {
            var task = await _context.TaskItems
                .Include(t => t.Project)
                .Include(t => t.Description)
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.TaskId == taskId);

            if (task == null)
                return NotFound();

            var dto = new TaskItemDto
            {
                ProjectId = task.ProjectId,
                TaskId = task.TaskId,
                TaskName = task.TaskName,
                EstimatedHours = task.EstimatedHours,
                DescriptionId = task.DescriptionId,
                Project = task.Project != null ? new ProjectDto { ProjectId = task.Project.ProjectId, ProjectName = task.Project.ProjectName } : null,
                Description = task.Description != null ? new TaskDescriptionDto { DescriptionId = task.Description.DescriptionId, TaskDescriptionText = task.Description.TaskDescriptionText } : null
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<TaskItemDto>> Create(TaskItemCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ProjectId) ||
                string.IsNullOrWhiteSpace(dto.TaskId) ||
                string.IsNullOrWhiteSpace(dto.TaskName) ||
                dto.EstimatedHours is null || dto.EstimatedHours <= 0 ||
                string.IsNullOrWhiteSpace(dto.DescriptionId))
                return BadRequest("ProjectId, TaskId, TaskName, DescriptionId and valid EstimatedHours are required");

            var projectExists = await _context.Projects.AnyAsync(p => p.ProjectId == dto.ProjectId);
            if (!projectExists)
                return BadRequest("Project does not exist");

            var descriptionExists = await _context.Descriptions.AnyAsync(d => d.DescriptionId == dto.DescriptionId);
            if (!descriptionExists)
                return BadRequest("Task description does not exist");

            var task = new TaskItem
            {
                ProjectId = dto.ProjectId,
                TaskId = dto.TaskId,
                TaskName = dto.TaskName,
                EstimatedHours = dto.EstimatedHours,
                DescriptionId = dto.DescriptionId
            };

            _context.TaskItems.Add(task);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (await _context.TaskItems.AnyAsync(t => t.ProjectId == dto.ProjectId && t.TaskId == dto.TaskId))
                    return BadRequest("Task with this ProjectId and TaskId combination already exists");
                throw;
            }

            await _context.Entry(task).Reference(t => t.Project).LoadAsync();
            await _context.Entry(task).Reference(t => t.Description).LoadAsync();

            var resultDto = new TaskItemDto
            {
                ProjectId = task.ProjectId,
                TaskId = task.TaskId,
                TaskName = task.TaskName,
                EstimatedHours = task.EstimatedHours,
                DescriptionId = task.DescriptionId,
                Project = task.Project != null ? new ProjectDto { ProjectId = task.Project.ProjectId, ProjectName = task.Project.ProjectName } : null,
                Description = task.Description != null ? new TaskDescriptionDto { DescriptionId = task.Description.DescriptionId, TaskDescriptionText = task.Description.TaskDescriptionText } : null
            };

            return CreatedAtAction(nameof(GetById), new { projectId = task.ProjectId, taskId = task.TaskId }, resultDto);
        }

        [HttpPut("{projectId}/{taskId}")]
        public async Task<IActionResult> Update(string projectId, string taskId, TaskItemUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TaskName) ||
                dto.EstimatedHours is null || dto.EstimatedHours <= 0 ||
                string.IsNullOrWhiteSpace(dto.DescriptionId))
                return BadRequest("TaskName, DescriptionId and valid EstimatedHours are required");

            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.ProjectId == projectId && t.TaskId == taskId);
            if (task == null)
                return NotFound();

            var descriptionExists = await _context.Descriptions.AnyAsync(d => d.DescriptionId == dto.DescriptionId);
            if (!descriptionExists)
                return BadRequest("Task description does not exist");

            task.TaskName = dto.TaskName;
            task.EstimatedHours = dto.EstimatedHours;
            task.DescriptionId = dto.DescriptionId;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{projectId}/{taskId}")]
        public async Task<IActionResult> Delete(string projectId, string taskId)
        {
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.ProjectId == projectId && t.TaskId == taskId);
            if (task == null)
                return NotFound();

            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static TaskItemDto ToDto(TaskItem task) => new()
        {
            ProjectId = task.ProjectId,
            TaskId = task.TaskId,
            TaskName = task.TaskName,
            EstimatedHours = task.EstimatedHours,
            DescriptionId = task.DescriptionId,
            Project = task.Project != null ? new ProjectDto
            {
                ProjectId = task.Project.ProjectId,
                ProjectName = task.Project.ProjectName
            } : null,
            Description = task.Description != null ? new TaskDescriptionDto
            {
                DescriptionId = task.Description.DescriptionId,
                TaskDescriptionText = task.Description.TaskDescriptionText
            } : null
        };
    }
}
