using System.ComponentModel.DataAnnotations;

namespace TrelloClone.Shared.Models;

// ── Role ────────────────────────────────────────────────
public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RolePermission> RolePermissions { get; set; } = [];
}

// ── Permission (link to code; codes themselves live in Permissions registry) ──
public class Permission
{
    [Required, MaxLength(80)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Category { get; set; } = string.Empty;
}

// ── Many-to-many: Role ↔ Permission ─────────────────────
public class RolePermission
{
    public Guid RoleId { get; set; }

    [Required, MaxLength(80)]
    public string PermissionCode { get; set; } = string.Empty;
}

// ── Many-to-many: User ↔ Role ───────────────────────────
public class UserRole
{
    public string UserId { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
}

// ── Static registry of permission codes ─────────────────
public static class Permissions
{
    // Portal access
    public const string AdminAccess = "admin.access";

    // Users
    public const string UsersView = "users.view";
    public const string UsersCreate = "users.create";
    public const string UsersUpdate = "users.update";
    public const string UsersDelete = "users.delete";
    public const string UsersPasswordReset = "users.password_reset";

    // Roles
    public const string RolesView = "roles.view";
    public const string RolesManage = "roles.manage";
    public const string RolesAssign = "roles.assign";

    public static readonly IReadOnlyList<PermissionDescriptor> All = new[]
    {
        new PermissionDescriptor(AdminAccess,        "Доступ к административной панели", "Portals"),

        new PermissionDescriptor(UsersView,          "Просмотр пользователей",           "Users"),
        new PermissionDescriptor(UsersCreate,        "Создание пользователей",           "Users"),
        new PermissionDescriptor(UsersUpdate,        "Редактирование пользователей",     "Users"),
        new PermissionDescriptor(UsersDelete,        "Удаление пользователей",           "Users"),
        new PermissionDescriptor(UsersPasswordReset, "Сброс пароля пользователя",        "Users"),

        new PermissionDescriptor(RolesView,          "Просмотр ролей",                   "Roles"),
        new PermissionDescriptor(RolesManage,        "Управление ролями",                "Roles"),
        new PermissionDescriptor(RolesAssign,        "Назначение ролей пользователям",   "Roles"),
    };
}

public sealed record PermissionDescriptor(string Code, string Description, string Category);

// ── System role names ───────────────────────────────────
public static class SystemRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string User       = "User";
}
