using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Data;
using EmployeeManagement.Entities;
using EmployeeManagement.DTOs;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Authorization;

namespace EmployeeManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IUserRoleService _userRoleService;
        private readonly IAllocationService _allocationService;
        private readonly IAccessScopeService _accessScope;

        public TasksController(AppDbContext context, IUserRoleService userRoleService, IAllocationService allocationService, IAccessScopeService accessScope)
        {
            _context = context;
            _userRoleService = userRoleService;
            _allocationService = allocationService;
            _accessScope = accessScope;
        }

        [HttpGet]
        [Authorize(Roles = RoleNames.Admin)]
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
            if (!await _accessScope.CanUseViewerIdAsync(User, viewerEmployeeId))
                return Forbid();

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
            if (!await _accessScope.CanViewTaskAsync(User, projectId, taskId))
                return Forbid();

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
        [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
        public async Task<ActionResult<IEnumerable<TaskStaffingResponse>>> GetStaffing([FromQuery] DateTime startDate, [FromQuery] DateTime? endDate, [FromQuery] string? projectId, [FromQuery] decimal hoursPerDay = 1)
        {
            if (startDate == default)
                return BadRequest("StartDate is required.");

            var end = (endDate ?? startDate).Date;
            if (startDate.Date > end)
                return BadRequest("EndDate cannot be before StartDate.");
            if (hoursPerDay <= 0)
                return BadRequest("HoursPerDay must be greater than zero.");

            List<string>? managedProjectIds = null;
            if (User.IsInRole(RoleNames.Manager))
            {
                var employeeId = User.FindFirst("employee_id")?.Value;
                managedProjectIds = string.IsNullOrWhiteSpace(employeeId)
                    ? new List<string>()
                    : await _context.ProjectManagers.Where(pm => pm.EmployeeId == employeeId).Select(pm => pm.ProjectId).ToListAsync();
                if (!string.IsNullOrWhiteSpace(projectId) && !managedProjectIds.Contains(projectId))
                    return Forbid();
            }

            var query = _context.TaskItems
                .AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.RequiredSkill)
                .Include(t => t.Allocations)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(projectId))
                query = query.Where(t => t.ProjectId == projectId);
            else if (managedProjectIds != null)
                query = query.Where(t => managedProjectIds.Contains(t.ProjectId));

            var tasks = await query
                .OrderBy(t => t.ProjectId)
                .ThenBy(t => t.TaskName)
                .ToListAsync();

            var result = new List<TaskStaffingResponse>();
            foreach (var task in tasks)
            {
                var taskStart = task.PlannedStartDate?.Date ?? startDate.Date;
                var taskEnd = task.PlannedEndDate?.Date ?? end;
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
                        StartDate = taskStart,
                        EndDate = taskEnd,
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
                    PlannedStartDate = task.PlannedStartDate,
                    PlannedEndDate = task.PlannedEndDate,
                    AllocatedPeople = task.Allocations.Count,
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

        [HttpPost("planning-preview")]
        [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
        public async Task<ActionResult<TaskPlanningPreviewResponse>> PreviewPlanning(TaskPlanningPreviewRequest request)
        {
            var validationError = ValidatePlanningRequest(request.ProjectId, request.EstimatedHours, request.PlannedStartDate, request.PlannedEndDate);
            if (validationError != null)
                return BadRequest(validationError);
            if (!await _context.Projects.AnyAsync(project => project.ProjectId == request.ProjectId))
                return BadRequest("Project does not exist.");
            if (!await CanManageProjectAsync(request.ProjectId))
                return Forbid();
            if (!string.IsNullOrWhiteSpace(request.RequiredSkillId) && !await _context.Skills.AnyAsync(skill => skill.SkillId == request.RequiredSkillId))
                return BadRequest("Required skill does not exist.");

            return Ok(await _allocationService.BuildTaskPlanAsync(request));
        }

        [HttpPost("create-planned")]
        [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
        public async Task<ActionResult<CreatePlannedTaskResponse>> CreatePlanned(CreatePlannedTaskRequest request)
        {
            var validationError = ValidatePlanningRequest(request.ProjectId, request.EstimatedHours, request.PlannedStartDate, request.PlannedEndDate);
            if (validationError != null)
                return BadRequest(validationError);
            if (string.IsNullOrWhiteSpace(request.TaskId) || string.IsNullOrWhiteSpace(request.TaskName) ||
                string.IsNullOrWhiteSpace(request.DescriptionId) || string.IsNullOrWhiteSpace(request.DescriptionText))
                return BadRequest("TaskId, TaskName, DescriptionId and DescriptionText are required.");
            if (!string.Equals(request.AllocationMode, "Automatic", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(request.AllocationMode, "Manual", StringComparison.OrdinalIgnoreCase))
                return BadRequest("AllocationMode must be Automatic or Manual.");
            if (!await _context.Projects.AnyAsync(project => project.ProjectId == request.ProjectId))
                return BadRequest("Project does not exist.");
            if (!await CanManageProjectAsync(request.ProjectId))
                return Forbid();
            if (!string.IsNullOrWhiteSpace(request.RequiredSkillId) && !await _context.Skills.AnyAsync(skill => skill.SkillId == request.RequiredSkillId))
                return BadRequest("Required skill does not exist.");
            if (await _context.TaskItems.AnyAsync(task => task.ProjectId == request.ProjectId && task.TaskId == request.TaskId))
                return BadRequest("Task with this ProjectId and TaskId combination already exists.");
            if (await _context.Descriptions.AnyAsync(description => description.DescriptionId == request.DescriptionId))
                return BadRequest("DescriptionId already exists.");
            if (request.ManualAllocations.GroupBy(item => item.EmployeeId).Any(group => group.Count() > 1))
                return BadRequest("An employee can only be allocated once to the same task.");

            var preview = await _allocationService.BuildTaskPlanAsync(new TaskPlanningPreviewRequest
            {
                ProjectId = request.ProjectId,
                EstimatedHours = request.EstimatedHours,
                RequiredSkillId = request.RequiredSkillId,
                PlannedStartDate = request.PlannedStartDate,
                PlannedEndDate = request.PlannedEndDate
            });

            var plannedAllocations = string.Equals(request.AllocationMode, "Automatic", StringComparison.OrdinalIgnoreCase)
                ? preview.AutomaticPlan.Select(item => new ManualTaskAllocationRequest
                {
                    EmployeeId = item.EmployeeId,
                    HoursPerDay = item.HoursPerDay
                }).ToList()
                : request.ManualAllocations;

            if (plannedAllocations.Any(item => string.IsNullOrWhiteSpace(item.EmployeeId) || item.HoursPerDay <= 0))
                return BadRequest("Each manual allocation requires an employee and hours per day greater than zero.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var description = new TaskDescription
                {
                    DescriptionId = request.DescriptionId,
                    TaskDescriptionText = request.DescriptionText
                };
                var task = new TaskItem
                {
                    ProjectId = request.ProjectId,
                    TaskId = request.TaskId,
                    TaskName = request.TaskName,
                    EstimatedHours = request.EstimatedHours,
                    DescriptionId = request.DescriptionId,
                    RequiredSkillId = string.IsNullOrWhiteSpace(request.RequiredSkillId) ? null : request.RequiredSkillId,
                    PlannedStartDate = request.PlannedStartDate.Date,
                    PlannedEndDate = request.PlannedEndDate.Date
                };

                _context.Descriptions.Add(description);
                _context.TaskItems.Add(task);
                await _context.SaveChangesAsync();

                var createdAllocations = new List<AllocationResponse>();
                foreach (var planned in plannedAllocations)
                {
                    var allocationResult = await _allocationService.CreateAllocationAsync(new CreateAllocationRequest
                    {
                        EmployeeId = planned.EmployeeId,
                        ProjectId = request.ProjectId,
                        TaskId = request.TaskId,
                        AllocationStartDate = request.PlannedStartDate.Date,
                        AllocationEndDate = request.PlannedEndDate.Date,
                        AllocatedHours = planned.HoursPerDay
                    });
                    if (!allocationResult.Success || allocationResult.Allocation == null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(allocationResult.Error);
                    }
                    createdAllocations.Add(allocationResult.Allocation);
                }

                await transaction.CommitAsync();
                await _context.Entry(task).Reference(item => item.Project).LoadAsync();
                await _context.Entry(task).Reference(item => item.Description).LoadAsync();
                await _context.Entry(task).Reference(item => item.RequiredSkill).LoadAsync();

                var allocatedHours = createdAllocations.Sum(item => item.TotalAllocationHours);
                var remainingHours = Math.Max(request.EstimatedHours - allocatedHours, 0);
                return CreatedAtAction(nameof(GetById), new { projectId = task.ProjectId, taskId = task.TaskId }, new CreatePlannedTaskResponse
                {
                    Task = ToDto(task),
                    Allocations = createdAllocations,
                    AllocatedHours = allocatedHours,
                    RemainingHours = remainingHours,
                    StaffingStatus = remainingHours <= 0.05m ? "Fully staffed" : createdAllocations.Count == 0 ? "Unstaffed" : "Partially staffed"
                });
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                return BadRequest("The task, description or allocation conflicts with existing data.");
            }
        }

        [HttpPost]
        [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
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
            if (!await CanManageProjectAsync(dto.ProjectId))
                return Forbid();
            if (dto.PlannedStartDate.HasValue && dto.PlannedEndDate.HasValue && dto.PlannedStartDate.Value.Date > dto.PlannedEndDate.Value.Date)
                return BadRequest("PlannedEndDate cannot be before PlannedStartDate.");

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
                RequiredSkillId = string.IsNullOrWhiteSpace(dto.RequiredSkillId) ? null : dto.RequiredSkillId,
                PlannedStartDate = dto.PlannedStartDate?.Date,
                PlannedEndDate = dto.PlannedEndDate?.Date
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
        [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
        public async Task<IActionResult> Update(string projectId, string taskId, TaskItemUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TaskName) ||
                dto.EstimatedHours is null || dto.EstimatedHours <= 0 ||
                string.IsNullOrWhiteSpace(dto.DescriptionId))
                return BadRequest("TaskName, DescriptionId and valid EstimatedHours are required");

            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.ProjectId == projectId && t.TaskId == taskId);
            if (task == null)
                return NotFound();
            if (!await CanManageProjectAsync(projectId))
                return Forbid();
            if (dto.PlannedStartDate.HasValue && dto.PlannedEndDate.HasValue && dto.PlannedStartDate.Value.Date > dto.PlannedEndDate.Value.Date)
                return BadRequest("PlannedEndDate cannot be before PlannedStartDate.");

            var descriptionExists = await _context.Descriptions.AnyAsync(d => d.DescriptionId == dto.DescriptionId);
            if (!descriptionExists)
                return BadRequest("Task description does not exist");
            if (!string.IsNullOrWhiteSpace(dto.RequiredSkillId) && !await _context.Skills.AnyAsync(s => s.SkillId == dto.RequiredSkillId))
                return BadRequest("Required skill does not exist");

            task.TaskName = dto.TaskName;
            task.EstimatedHours = dto.EstimatedHours;
            task.DescriptionId = dto.DescriptionId;
            task.RequiredSkillId = string.IsNullOrWhiteSpace(dto.RequiredSkillId) ? null : dto.RequiredSkillId;
            task.PlannedStartDate = dto.PlannedStartDate?.Date;
            task.PlannedEndDate = dto.PlannedEndDate?.Date;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{projectId}/{taskId}")]
        [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
        public async Task<IActionResult> Delete(string projectId, string taskId)
        {
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.ProjectId == projectId && t.TaskId == taskId);
            if (task == null)
                return NotFound();
            if (!await CanManageProjectAsync(projectId))
                return Forbid();

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
            PlannedStartDate = task.PlannedStartDate,
            PlannedEndDate = task.PlannedEndDate,
            RequiredSkill = task.RequiredSkill != null ? new SkillDto
            {
                SkillId = task.RequiredSkill.SkillId,
                SkillName = task.RequiredSkill.SkillName,
                SkillLevel = task.RequiredSkill.SkillLevel
            } : null
        };

        private async Task<bool> CanManageProjectAsync(string projectId)
        {
            return await _accessScope.CanManageProjectAsync(User, projectId);
        }

        private string? ValidatePlanningRequest(string projectId, decimal estimatedHours, DateTime startDate, DateTime endDate)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                return "ProjectId is required.";
            if (estimatedHours <= 0)
                return "EstimatedHours must be greater than zero.";
            if (startDate == default || endDate == default)
                return "PlannedStartDate and PlannedEndDate are required.";
            if (startDate.Date > endDate.Date)
                return "PlannedEndDate cannot be before PlannedStartDate.";
            if (_allocationService.CountWorkingDays(startDate, endDate) == 0)
                return "The planned interval must contain at least one working day.";
            return null;
        }
    }
}
