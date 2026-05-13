using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrelloClone.Api.Data;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = Permissions.AdminAccess)]
public class AdminUsersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.UsersView)]
    public async Task<ActionResult<List<AdminUserDto>>> List()
    {
        var users = await db.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
        var ids   = users.Select(u => u.Id).ToList();

        var userRoles = await (from ur in db.UserRoles
                               join r  in db.Roles on ur.RoleId equals r.Id
                               where ids.Contains(ur.UserId)
                               select new { ur.UserId, Role = new RoleSummaryDto(r.Id, r.Name, r.IsSystem) })
                              .ToListAsync();

        var rolesByUser = userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Role).ToList());

        return users
            .Select(u => new AdminUserDto(
                u.Id, u.UserName, u.Email, u.IsActive, u.CreatedAt,
                rolesByUser.TryGetValue(u.Id, out var roles) ? roles : []))
            .ToList();
    }

    [HttpGet("{id}")]
    [Authorize(Policy = Permissions.UsersView)]
    public async Task<ActionResult<AdminUserDto>> Get(string id)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        var roles = await (from ur in db.UserRoles
                           join r in db.Roles on ur.RoleId equals r.Id
                           where ur.UserId == id
                           select new RoleSummaryDto(r.Id, r.Name, r.IsSystem))
                          .ToListAsync();

        return new AdminUserDto(user.Id, user.UserName, user.Email, user.IsActive, user.CreatedAt, roles);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.UsersCreate)]
    public async Task<ActionResult<AdminUserDto>> Create(AdminCreateUserRequest req)
    {
        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            return BadRequest(new { error = "Email already registered" });

        var user = new AppUser
        {
            UserName     = req.UserName,
            Email        = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            IsActive     = true
        };
        db.Users.Add(user);

        if (req.RoleIds is { Count: > 0 })
        {
            var validRoleIds = await db.Roles
                .Where(r => req.RoleIds.Contains(r.Id))
                .Select(r => r.Id).ToListAsync();
            foreach (var roleId in validRoleIds)
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        }

        await db.SaveChangesAsync();
        return await Get(user.Id);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Permissions.UsersUpdate)]
    public async Task<ActionResult<AdminUserDto>> Update(string id, AdminUpdateUserRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        if (user.Email != req.Email &&
            await db.Users.AnyAsync(u => u.Email == req.Email && u.Id != id))
            return BadRequest(new { error = "Email already used by another user" });

        user.UserName = req.UserName;
        user.Email    = req.Email;
        user.IsActive = req.IsActive;

        if (req.RoleIds is not null)
        {
            var current = await db.UserRoles.Where(ur => ur.UserId == id).ToListAsync();
            db.UserRoles.RemoveRange(current);

            var validRoleIds = await db.Roles
                .Where(r => req.RoleIds.Contains(r.Id))
                .Select(r => r.Id).ToListAsync();
            foreach (var roleId in validRoleIds)
                db.UserRoles.Add(new UserRole { UserId = id, RoleId = roleId });
        }

        await db.SaveChangesAsync();
        return await Get(id);
    }

    [HttpPost("{id}/password")]
    [Authorize(Policy = Permissions.UsersPasswordReset)]
    public async Task<IActionResult> ResetPassword(string id, AdminResetPasswordRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Permissions.UsersDelete)]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        var hasSuperAdmin = await db.UserRoles
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .AnyAsync(x => x.UserId == id && x.Name == SystemRoles.SuperAdmin);

        if (hasSuperAdmin)
        {
            var totalAdmins = await db.UserRoles
                .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                .CountAsync(name => name == SystemRoles.SuperAdmin);
            if (totalAdmins <= 1)
                return BadRequest(new { error = "Cannot delete the last super-admin" });
        }

        db.UserRoles.RemoveRange(db.UserRoles.Where(ur => ur.UserId == id));
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
