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
        private readonly IAllocationService _allocationService;

        public TasksController(AppDbContext context, IUserRoleService userRoleService, IAllocationService allocationService)
        {
            _context = context;
            _userRoleService = userRoleService;
            _allocationService = allocationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskItemDto>>> GetAll()
        {
            var tasks = await _context.TaskItems
                .Include(t => t.Project)
                .Include(t => t.Description)
                .Include(t => t.RequiredSkill)
                .ToListAsync();

            var dtos = tasks.Select(ToDto).ToList();

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
                .Include(t => t.Description)
                .Include(t => t.RequiredSkill);

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
                .Include(t => t.RequiredSkill)
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.TaskId == taskId);

            if (task == null)
                return NotFound();

            return Ok(ToDto(task));
        }

        [HttpGet("staffing")]
        public async Task<ActionResult<IEnumerable<TaskStaffingResponse>>> GetStaffing([FromQuery] DateTime startDate, [FromQuery] DateTime? endDate, [FromQuery] string? projectId, [FromQuery] decimal hoursPerDay = 1)
        {
            if (startDate == default)
                return BadRequest("StartDate is required.");

            var end = (endDate ?? startDate).Date;
            if (startDate.Date > end)
                return BadRequest("EndDate cannot be before StartDate.");
            if (hoursPerDay <= 0)
                return BadRequest("HoursPerDay must be greater than zero.");

            var query = _context.TaskItems
                .AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.RequiredSkill)
                .Include(t => t.Allocations)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(projectId))
                query = query.Where(t => t.ProjectId == projectId);

            var tasks = await query
                .OrderBy(t => t.ProjectId)
                .ThenBy(t => t.TaskName)
                .ToListAsync();

            var result = new List<TaskStaffingResponse>();
            foreach (var task in tasks)
            {
                var estimated = task.EstimatedHours ?? 0;
                var allocated = task.Allocations.Sum(a => _allocationService.CalculateTotalAllocationHours(
                    a.AllocationStartDate,
                    a.AllocationEndDate ?? a.AllocationStartDate,
                    a.AllocatedHours));
                var remaining = Math.Max(estimated - allocated, 0);
                var candidates = remaining <= 0
                    ? new List<AllocationAvailabilityResponse>()
                    : (await _allocationService.GetAvailabilityAsync(new AllocationAvailabilityRequest
                    {
                        ProjectId = task.ProjectId,
                        SkillId = task.RequiredSkillId,
                        StartDate = startDate.Date,
                        EndDate = end,
                        RequiredHoursPerDay = hoursPerDay
                    })).Where(c => c.CanTakeRequestedHours).Take(5).ToList();

                result.Add(new TaskStaffingResponse
                {
                    ProjectId = task.ProjectId,
                    ProjectName = task.Project?.ProjectName ?? task.ProjectId,
                    TaskId = task.TaskId,
                    TaskName = task.TaskName,
                    EstimatedHours = estimated,
                    AllocatedHours = allocated,
                    RemainingHours = remaining,
                    RequiredSkillId = task.RequiredSkillId,
                    RequiredSkillName = task.RequiredSkill?.SkillName,
                    RequiredSkillLevel = task.RequiredSkill?.SkillLevel,
                    Status = remaining <= 0
                        ? "Fully staffed"
                        : candidates.Any()
                            ? "Needs more allocation"
                            : "No available qualified employee",
                    Candidates = candidates
                });
            }

            return Ok(result.OrderByDescending(x => x.RemainingHours).ThenBy(x => x.ProjectName).ThenBy(x => x.TaskName));
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
            if (!string.IsNullOrWhiteSpace(dto.RequiredSkillId) && !await _context.Skills.AnyAsync(s => s.SkillId == dto.RequiredSkillId))
                return BadRequest("Required skill does not exist");

            var task = new TaskItem
            {
                ProjectId = dto.ProjectId,
                TaskId = dto.TaskId,
                TaskName = dto.TaskName,
                EstimatedHours = dto.EstimatedHours,
                DescriptionId = dto.DescriptionId,
                RequiredSkillId = string.IsNullOrWhiteSpace(dto.RequiredSkillId) ? null : dto.RequiredSkillId
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
            await _context.Entry(task).Reference(t => t.RequiredSkill).LoadAsync();

            var resultDto = ToDto(task);

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
            if (!string.IsNullOrWhiteSpace(dto.RequiredSkillId) && !await _context.Skills.AnyAsync(s => s.SkillId == dto.RequiredSkillId))
                return BadRequest("Required skill does not exist");

            task.TaskName = dto.TaskName;
            task.EstimatedHours = dto.EstimatedHours;
            task.DescriptionId = dto.DescriptionId;
            task.RequiredSkillId = string.IsNullOrWhiteSpace(dto.RequiredSkillId) ? null : dto.RequiredSkillId;

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
            } : null,
            RequiredSkillId = task.RequiredSkillId,
            RequiredSkill = task.RequiredSkill != null ? new SkillDto
            {
                SkillId = task.RequiredSkill.SkillId,
                SkillName = task.RequiredSkill.SkillName,
                SkillLevel = task.RequiredSkill.SkillLevel
            } : null
        };
    }
}
