using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Services;

namespace EmployeeManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaskCommentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAccessScopeService _accessScope;
    public TaskCommentsController(AppDbContext context, IAccessScopeService accessScope)
    {
        _context = context;
        _accessScope = accessScope;
    }

    [HttpGet("task/{projectId}/{taskId}")]
    public async Task<IActionResult> GetByTask(string projectId, string taskId)
    {
        if (!await _accessScope.CanViewTaskAsync(User, projectId, taskId))
            return Forbid();
        return Ok(await _context.TaskComments.Where(c => c.ProjectId == projectId && c.TaskId == taskId).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create(TaskCommentRequest request)
    {
        if (!await _accessScope.CanViewTaskAsync(User, request.ProjectId, request.TaskId))
            return Forbid();

        if (!await _context.TaskItems.AnyAsync(t => t.ProjectId == request.ProjectId && t.TaskId == request.TaskId)) return BadRequest("Invalid task.");
        if (!string.IsNullOrWhiteSpace(request.EmployeeId) && !await _context.Employees.AnyAsync(e => e.EmployeeId == request.EmployeeId)) return BadRequest("Invalid employee.");

        var comment = new TaskComment
        {
            TaskCommentId = request.TaskCommentId,
            CommentText = request.CommentText,
            CommentDate = request.CommentDate ?? DateTime.Today,
            ProjectId = request.ProjectId,
            TaskId = request.TaskId,
            EmployeeId = User.IsInRole(RoleNames.Admin) && !string.IsNullOrWhiteSpace(request.EmployeeId)
                ? request.EmployeeId
                : _accessScope.GetCurrentEmployeeId(User)
        };

        _context.TaskComments.Add(comment);
        await _context.SaveChangesAsync();
        return Ok(comment);
    }

    [HttpPut("{taskCommentId}")]
    public async Task<IActionResult> Update(string taskCommentId, UpdateTaskCommentRequest request)
    {
        var comment = await _context.TaskComments.FindAsync(taskCommentId);
        if (comment == null) return NotFound();
        if (!CanEditComment(comment)) return Forbid();
        comment.CommentText = request.CommentText;
        await _context.SaveChangesAsync();
        return Ok(comment);
    }

    [HttpDelete("{taskCommentId}")]
    public async Task<IActionResult> Delete(string taskCommentId)
    {
        var comment = await _context.TaskComments.FindAsync(taskCommentId);
        if (comment == null) return NotFound();
        if (!CanEditComment(comment) && !await _accessScope.CanManageProjectAsync(User, comment.ProjectId)) return Forbid();
        _context.TaskComments.Remove(comment);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private bool CanEditComment(TaskComment comment) =>
        User.IsInRole(RoleNames.Admin) || comment.EmployeeId == _accessScope.GetCurrentEmployeeId(User);
}
