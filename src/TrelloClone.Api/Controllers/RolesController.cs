using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrelloClone.Api.Data;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController]
[Route("api/admin/roles")]
[Authorize(Policy = Permissions.AdminAccess)]
public class RolesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.RolesView)]
    public async Task<ActionResult<List<RoleDto>>> List()
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
        var ids   = roles.Select(r => r.Id).ToList();

        var perms = await db.RolePermissions
            .Where(rp => ids.Contains(rp.RoleId))
            .ToListAsync();
        var permsByRole = perms.GroupBy(rp => rp.RoleId)
            .ToDictionary(g => g.Key, g => g.Select(rp => rp.PermissionCode).ToList());

        return roles
            .Select(r => new RoleDto(
                r.Id, r.Name, r.Description, r.IsSystem,
                permsByRole.TryGetValue(r.Id, out var p) ? p : []))
            .ToList();
    }

    [HttpGet("permissions")]
    [Authorize(Policy = Permissions.RolesView)]
    public ActionResult<List<PermissionDescriptor>> AllPermissions() =>
        Shared.Models.Permissions.All.ToList();

    [HttpGet("{id}")]
    [Authorize(Policy = Permissions.RolesView)]
    public async Task<ActionResult<RoleDto>> Get(Guid id)
    {
        var role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();

        var perms = await db.RolePermissions
            .Where(rp => rp.RoleId == id)
            .Select(rp => rp.PermissionCode)
            .ToListAsync();

        return new RoleDto(role.Id, role.Name, role.Description, role.IsSystem, perms);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.RolesManage)]
    public async Task<ActionResult<RoleDto>> Create(CreateRoleRequest req)
    {
        if (await db.Roles.AnyAsync(r => r.Name == req.Name))
            return BadRequest(new { error = "Role name already exists" });

        var role = new Role
        {
            Name = req.Name,
            Description = req.Description,
            IsSystem = false
        };
        db.Roles.Add(role);

        if (req.Permissions is { Count: > 0 })
        {
            var validCodes = Shared.Models.Permissions.All.Select(p => p.Code).ToHashSet();
            foreach (var code in req.Permissions.Where(validCodes.Contains).Distinct())
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = code });
        }

        await db.SaveChangesAsync();
        return await Get(role.Id);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Permissions.RolesManage)]
    public async Task<ActionResult<RoleDto>> Update(Guid id, UpdateRoleRequest req)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();

        if (role.IsSystem && role.Name == SystemRoles.SuperAdmin)
            return BadRequest(new { error = "Cannot modify SuperAdmin role" });

        if (role.Name != req.Name && await db.Roles.AnyAsync(r => r.Name == req.Name && r.Id != id))
            return BadRequest(new { error = "Role name already used" });

        if (!role.IsSystem)
            role.Name = req.Name;
        role.Description = req.Description;

        if (req.Permissions is not null)
        {
            var current = await db.RolePermissions.Where(rp => rp.RoleId == id).ToListAsync();
            db.RolePermissions.RemoveRange(current);

            var validCodes = Shared.Models.Permissions.All.Select(p => p.Code).ToHashSet();
            foreach (var code in req.Permissions.Where(validCodes.Contains).Distinct())
                db.RolePermissions.Add(new RolePermission { RoleId = id, PermissionCode = code });
        }

        await db.SaveChangesAsync();
        return await Get(id);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Permissions.RolesManage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();
        if (role.IsSystem) return BadRequest(new { error = "Cannot delete a system role" });

        db.UserRoles.RemoveRange(db.UserRoles.Where(ur => ur.RoleId == id));
        db.Roles.Remove(role);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
