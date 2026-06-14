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
    public class DepartmentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DepartmentsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetAll()
        {
            var departments = await _context.Departments.ToListAsync();
            var dtos = departments.Select(d => new DepartmentDto { DepartmentId = d.DepartmentId, DepartmentName = d.DepartmentName }).ToList();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DepartmentDto>> GetById(string id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null)
                return NotFound();

            return Ok(new DepartmentDto { DepartmentId = department.DepartmentId, DepartmentName = department.DepartmentName });
        }

        [HttpPost]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<DepartmentDto>> Create(DepartmentCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.DepartmentId) || string.IsNullOrWhiteSpace(dto.DepartmentName))
                return BadRequest("DepartmentId and DepartmentName are required");

            var department = new Department
            {
                DepartmentId = dto.DepartmentId,
                DepartmentName = dto.DepartmentName
            };

            _context.Departments.Add(department);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (await _context.Departments.AnyAsync(d => d.DepartmentId == dto.DepartmentId))
                    return BadRequest("Department with this ID already exists");
                throw;
            }

            var resultDto = new DepartmentDto { DepartmentId = department.DepartmentId, DepartmentName = department.DepartmentName };
            return CreatedAtAction(nameof(GetById), new { id = department.DepartmentId }, resultDto);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Update(string id, DepartmentUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.DepartmentName))
                return BadRequest("DepartmentName is required");

            var department = await _context.Departments.FindAsync(id);
            if (department == null)
                return NotFound();

            department.DepartmentName = dto.DepartmentName;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Delete(string id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null)
                return NotFound();

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
