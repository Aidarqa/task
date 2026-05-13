using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrelloClone.Api.Data;
using TrelloClone.Api.Services;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, ITokenService tokenService) : ControllerBase
{
    /// <summary>
    /// Registration is disabled for the public. New users are created from the admin panel
    /// via POST /api/admin/users (requires Permissions.UsersCreate).
    /// This endpoint is kept for backwards compatibility but requires the same permission.
    /// </summary>
    [HttpPost("register")]
    [Authorize(Policy = Permissions.UsersCreate)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            return BadRequest(new AuthResponse(false, null, null, null, "Email already registered"));

        var user = new AppUser
        {
            UserName     = req.UserName,
            Email        = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            IsActive     = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = await tokenService.GenerateTokenAsync(user);
        return Ok(new AuthResponse(true, token, user.Id, user.UserName, null));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new AuthResponse(false, null, null, null, "Invalid credentials"));

        if (!user.IsActive)
            return Unauthorized(new AuthResponse(false, null, null, null, "Account is disabled"));

        var token = await tokenService.GenerateTokenAsync(user);
        return Ok(new AuthResponse(true, token, user.Id, user.UserName, null));
    }
}
