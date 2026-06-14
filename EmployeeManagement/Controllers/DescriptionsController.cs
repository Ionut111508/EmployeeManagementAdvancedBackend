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
    public class DescriptionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DescriptionsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskDescriptionDto>>> GetAll()
        {
            var descriptions = await _context.Descriptions.ToListAsync();
            var dtos = descriptions.Select(d => new TaskDescriptionDto { DescriptionId = d.DescriptionId, TaskDescriptionText = d.TaskDescriptionText }).ToList();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TaskDescriptionDto>> GetById(string id)
        {
            var description = await _context.Descriptions.FindAsync(id);
            if (description == null)
                return NotFound();

            return Ok(new TaskDescriptionDto { DescriptionId = description.DescriptionId, TaskDescriptionText = description.TaskDescriptionText });
        }

        [HttpPost]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<TaskDescriptionDto>> Create(TaskDescriptionCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.DescriptionId) || string.IsNullOrWhiteSpace(dto.TaskDescriptionText))
                return BadRequest("DescriptionId and TaskDescriptionText are required");

            var description = new TaskDescription
            {
                DescriptionId = dto.DescriptionId,
                TaskDescriptionText = dto.TaskDescriptionText
            };

            _context.Descriptions.Add(description);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (await _context.Descriptions.AnyAsync(d => d.DescriptionId == dto.DescriptionId))
                    return BadRequest("Description with this ID already exists");
                throw;
            }

            var resultDto = new TaskDescriptionDto { DescriptionId = description.DescriptionId, TaskDescriptionText = description.TaskDescriptionText };
            return CreatedAtAction(nameof(GetById), new { id = description.DescriptionId }, resultDto);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Update(string id, TaskDescriptionUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TaskDescriptionText))
                return BadRequest("TaskDescriptionText is required");

            var description = await _context.Descriptions.FindAsync(id);
            if (description == null)
                return NotFound();

            description.TaskDescriptionText = dto.TaskDescriptionText;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Delete(string id)
        {
            var description = await _context.Descriptions.FindAsync(id);
            if (description == null)
                return NotFound();

            _context.Descriptions.Remove(description);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
