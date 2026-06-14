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
    public class SkillsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SkillsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SkillDto>>> GetAll()
        {
            var skills = await _context.Skills.ToListAsync();
            var dtos = skills.Select(s => new SkillDto
            {
                SkillId = s.SkillId,
                SkillName = s.SkillName,
                SkillLevel = s.SkillLevel
            }).ToList();

            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SkillDto>> GetById(string id)
        {
            var skill = await _context.Skills.FindAsync(id);
            if (skill == null) return NotFound();

            return Ok(new SkillDto
            {
                SkillId = skill.SkillId,
                SkillName = skill.SkillName,
                SkillLevel = skill.SkillLevel
            });
        }

        [HttpPost]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<SkillDto>> Create(SkillCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.SkillId) || string.IsNullOrWhiteSpace(dto.SkillName))
                return BadRequest("SkillId and SkillName are required.");

            var exists = await _context.Skills.AnyAsync(s => s.SkillId == dto.SkillId);
            if (exists) return BadRequest("Skill with this ID already exists.");

            var skill = new Skill
            {
                SkillId = dto.SkillId,
                SkillName = dto.SkillName,
                SkillLevel = dto.SkillLevel
            };

            _context.Skills.Add(skill);
            await _context.SaveChangesAsync();

            var resultDto = new SkillDto
            {
                SkillId = skill.SkillId,
                SkillName = skill.SkillName,
                SkillLevel = skill.SkillLevel
            };

            return CreatedAtAction(nameof(GetById), new { id = skill.SkillId }, resultDto);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Update(string id, SkillUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.SkillName))
                return BadRequest("SkillName is required.");

            var skill = await _context.Skills.FindAsync(id);
            if (skill == null) return NotFound();

            skill.SkillName = dto.SkillName;
            skill.SkillLevel = dto.SkillLevel;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Delete(string id)
        {
            var skill = await _context.Skills.FindAsync(id);
            if (skill == null) return NotFound();

            _context.Skills.Remove(skill);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
