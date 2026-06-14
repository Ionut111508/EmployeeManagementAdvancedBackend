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
    public class WorkNormsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WorkNormsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkNormDto>>> GetAll()
        {
            var workNorms = await _context.WorkNorms.ToListAsync();
            var dtos = workNorms.Select(w => new WorkNormDto { WorkNormId = w.WorkNormId, WorkNormName = w.WorkNormName, WorkHours = w.WorkHours }).ToList();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WorkNormDto>> GetById(string id)
        {
            var workNorm = await _context.WorkNorms.FindAsync(id);
            if (workNorm == null)
                return NotFound();

            return Ok(new WorkNormDto { WorkNormId = workNorm.WorkNormId, WorkNormName = workNorm.WorkNormName, WorkHours = workNorm.WorkHours });
        }

        [HttpPost]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<WorkNormDto>> Create(WorkNormCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.WorkNormId) || string.IsNullOrWhiteSpace(dto.WorkNormName) || dto.WorkHours <= 0)
                return BadRequest("WorkNormId, WorkNormName, and valid WorkHours are required");

            var workNorm = new WorkNorm
            {
                WorkNormId = dto.WorkNormId,
                WorkNormName = dto.WorkNormName,
                WorkHours = dto.WorkHours
            };

            _context.WorkNorms.Add(workNorm);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (await _context.WorkNorms.AnyAsync(w => w.WorkNormId == dto.WorkNormId))
                    return BadRequest("WorkNorm with this ID already exists");
                throw;
            }

            var resultDto = new WorkNormDto { WorkNormId = workNorm.WorkNormId, WorkNormName = workNorm.WorkNormName, WorkHours = workNorm.WorkHours };
            return CreatedAtAction(nameof(GetById), new { id = workNorm.WorkNormId }, resultDto);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Update(string id, WorkNormUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.WorkNormName) || dto.WorkHours <= 0)
                return BadRequest("WorkNormName and valid WorkHours are required");

            var workNorm = await _context.WorkNorms.FindAsync(id);
            if (workNorm == null)
                return NotFound();

            workNorm.WorkNormName = dto.WorkNormName;
            workNorm.WorkHours = dto.WorkHours;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Delete(string id)
        {
            var workNorm = await _context.WorkNorms.FindAsync(id);
            if (workNorm == null)
                return NotFound();

            _context.WorkNorms.Remove(workNorm);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
