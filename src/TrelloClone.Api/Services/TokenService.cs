using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TrelloClone.Api.Data;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Services;

public interface ITokenService
{
    Task<string> GenerateTokenAsync(AppUser user, CancellationToken ct = default);
}

public class TokenService : ITokenService
{
    public const string PermissionClaimType = "perm";

    private readonly IConfiguration _config;
    private readonly AppDbContext   _db;

    public TokenService(IConfiguration config, AppDbContext db)
    {
        _config = config;
        _db = db;
    }

    public async Task<string> GenerateTokenAsync(AppUser user, CancellationToken ct = default)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT Key not configured")));

        // Roles + permissions for this user
        var roleNames = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
            .ToListAsync(ct);

        var permissionCodes = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Join(_db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (_, rp) => rp.PermissionCode)
            .Distinct()
            .ToListAsync(ct);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name,           user.UserName),
            new(ClaimTypes.Email,          user.Email)
        };
        foreach (var role in roleNames)
            claims.Add(new Claim(ClaimTypes.Role, role));
        foreach (var perm in permissionCodes)
            claims.Add(new Claim(PermissionClaimType, perm));

        var token = new JwtSecurityToken(
            issuer:    _config["Jwt:Issuer"],
            audience:  _config["Jwt:Audience"],
            claims:    claims,
            expires:   DateTime.UtcNow.AddDays(7),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
