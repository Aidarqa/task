using System.ComponentModel.DataAnnotations;

namespace TrelloClone.Shared.Models;

// ── Admin: users ────────────────────────────────────────
public record AdminUserDto(
    string Id,
    string UserName,
    string Email,
    bool IsActive,
    DateTime CreatedAt,
    List<RoleSummaryDto> Roles
);

public record AdminCreateUserRequest(
    [Required, MaxLength(100)] string UserName,
    [Required, EmailAddress]   string Email,
    [Required, MinLength(6)]   string Password,
    List<Guid>? RoleIds
);

public record AdminUpdateUserRequest(
    [Required, MaxLength(100)] string UserName,
    [Required, EmailAddress]   string Email,
    bool IsActive,
    List<Guid>? RoleIds
);

public record AdminResetPasswordRequest(
    [Required, MinLength(6)] string NewPassword
);

// ── Admin: roles ────────────────────────────────────────
public record RoleSummaryDto(Guid Id, string Name, bool IsSystem);

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    List<string> Permissions
);

public record CreateRoleRequest(
    [Required, MaxLength(100)] string Name,
    [MaxLength(400)] string? Description,
    List<string>? Permissions
);

public record UpdateRoleRequest(
    [Required, MaxLength(100)] string Name,
    [MaxLength(400)] string? Description,
    List<string>? Permissions
);
