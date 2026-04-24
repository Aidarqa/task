using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrelloClone.Api.Data;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class EmployeesController(AppDbContext db) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET /api/employees
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await db.Users.ToListAsync();
        var profiles = await db.EmployeeProfiles
            .Include(p => p.Department)
            .ToDictionaryAsync(p => p.UserId);

        var result = users.Select(u =>
        {
            profiles.TryGetValue(u.Id, out var p);
            return new UserDto(u.Id, u.UserName, u.Email,
                p?.Position, p?.Phone, p?.DepartmentId,
                p?.Department?.Name, p?.Role ?? SystemRole.Employee,
                p?.IsActive ?? true, u.AvatarUrl);
        });

        return Ok(result);
    }

    // GET /api/employees/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var u = await db.Users.FindAsync(id);
        if (u is null) return NotFound();
        var p = await db.EmployeeProfiles.Include(x => x.Department).FirstOrDefaultAsync(x => x.UserId == id);
        return Ok(new UserDto(u.Id, u.UserName, u.Email,
            p?.Position, p?.Phone, p?.DepartmentId, p?.Department?.Name,
            p?.Role ?? SystemRole.Employee, p?.IsActive ?? true, u.AvatarUrl));
    }

    // PUT /api/employees/{id}/profile
    [HttpPut("{id}/profile")]
    public async Task<IActionResult> UpdateProfile(string id, UpdateProfileRequest req)
    {
        if (id != CurrentUserId && !await IsAdmin()) return Forbid();
        var profile = await db.EmployeeProfiles.FindAsync(id)
                      ?? new EmployeeProfile { UserId = id };
        profile.Position = req.Position;
        profile.Phone = req.Phone;
        profile.DepartmentId = req.DepartmentId;
        if (!db.EmployeeProfiles.Local.Contains(profile)) db.EmployeeProfiles.Add(profile);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // PUT /api/employees/{id}/role
    [HttpPut("{id}/role"), Authorize]
    public async Task<IActionResult> SetRole(string id, SetRoleRequest req)
    {
        if (!await IsAdmin()) return Forbid();
        var profile = await db.EmployeeProfiles.FindAsync(id)
                      ?? new EmployeeProfile { UserId = id };
        profile.Role = req.Role;
        if (!db.EmployeeProfiles.Local.Contains(profile)) db.EmployeeProfiles.Add(profile);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/employees/departments
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
        => Ok(await db.Departments.ToListAsync());

    // POST /api/employees/departments
    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment(CreateDepartmentRequest req)
    {
        var dept = new Department { Name = req.Name, Description = req.Description, ParentId = req.ParentId };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        return Ok(dept);
    }

    // DELETE /api/employees/departments/{id}
    [HttpDelete("departments/{id:guid}")]
    public async Task<IActionResult> DeleteDepartment(Guid id)
    {
        var dept = await db.Departments.FindAsync(id);
        if (dept is null) return NotFound();
        db.Departments.Remove(dept);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> IsAdmin()
    {
        var p = await db.EmployeeProfiles.FindAsync(CurrentUserId);
        return p?.Role == SystemRole.Admin;
    }
}
