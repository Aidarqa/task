using Microsoft.EntityFrameworkCore;
using TrelloClone.Api.Data;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Services;

public static class SchemaBootstrapper
{
    public static async Task EnsureLatestAsync(
        AppDbContext db,
        IConfiguration config,
        CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsNpgsql())
            return;

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "FileAttachments" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "FileName" character varying(255) NOT NULL,
                "StoragePath" character varying(500) NOT NULL,
                "ContentType" character varying(200) NOT NULL,
                "Size" bigint NOT NULL,
                "UploadedById" text NOT NULL,
                "UploadedByName" text NOT NULL,
                "UploadedAt" timestamp with time zone NOT NULL,
                "IsPinned" boolean NOT NULL,
                "WorkTaskId" uuid NULL,
                "ChatId" uuid NULL,
                "ChatMessageId" uuid NULL,
                CONSTRAINT "FK_FileAttachments_WorkTasks_WorkTaskId" FOREIGN KEY ("WorkTaskId") REFERENCES "WorkTasks" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_FileAttachments_ChatMessages_ChatMessageId" FOREIGN KEY ("ChatMessageId") REFERENCES "ChatMessages" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_FileAttachments_WorkTaskId" ON "FileAttachments" ("WorkTaskId");
            CREATE INDEX IF NOT EXISTS "IX_FileAttachments_ChatId" ON "FileAttachments" ("ChatId");
            CREATE INDEX IF NOT EXISTS "IX_FileAttachments_ChatMessageId" ON "FileAttachments" ("ChatMessageId");

            ALTER TABLE "TodoItems"
                ADD COLUMN IF NOT EXISTS "WorkTaskId" uuid NULL;

            CREATE INDEX IF NOT EXISTS "IX_TodoItems_WorkTaskId" ON "TodoItems" ("WorkTaskId");

            ALTER TABLE "Projects"
                ADD COLUMN IF NOT EXISTS "Visibility" integer NOT NULL DEFAULT 0;

            CREATE TABLE IF NOT EXISTS "ProjectMembers" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "ProjectId" uuid NOT NULL,
                "UserId" text NOT NULL,
                "UserName" text NOT NULL,
                CONSTRAINT "FK_ProjectMembers_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProjectMembers_ProjectId_UserId" ON "ProjectMembers" ("ProjectId", "UserId");

            ALTER TABLE "Notifications"
                ADD COLUMN IF NOT EXISTS "SenderUserId" text NULL;

            -- Increase Body column from varchar(500) to varchar(4000) for system broadcasts
            ALTER TABLE "Notifications"
                ALTER COLUMN "Body" TYPE character varying(4000);

            -- ── Visits ───────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS "Visits" (
                "Id"               uuid NOT NULL PRIMARY KEY,
                "Number"           character varying(20) NOT NULL DEFAULT '',
                "Status"           integer NOT NULL DEFAULT 0,
                "Purpose"          character varying(500) NOT NULL DEFAULT '',
                "PlannedArrival"   timestamp with time zone NOT NULL,
                "PlannedDeparture" timestamp with time zone NOT NULL,
                "HostUserId"       text NOT NULL DEFAULT '',
                "HostUserName"     text NOT NULL DEFAULT '',
                "ApproverId"       text NULL,
                "ApproverName"     text NULL,
                "ApproverComment"  character varying(1000) NULL,
                "ApprovedAt"       timestamp with time zone NULL,
                "ResourceId"       uuid NULL,
                "ResourceName"     character varying(200) NULL,
                "CreatedAt"        timestamp with time zone NOT NULL DEFAULT NOW()
            );
            CREATE INDEX IF NOT EXISTS "IX_Visits_HostUserId"  ON "Visits" ("HostUserId");
            CREATE INDEX IF NOT EXISTS "IX_Visits_ApproverId"  ON "Visits" ("ApproverId");

            CREATE TABLE IF NOT EXISTS "VisitGuests" (
                "Id"             uuid NOT NULL PRIMARY KEY,
                "VisitId"        uuid NOT NULL,
                "FullName"       character varying(200) NOT NULL DEFAULT '',
                "Organization"   character varying(200) NULL,
                "Phone"          character varying(50) NULL,
                "DocumentType"   integer NOT NULL DEFAULT 0,
                "DocumentNumber" character varying(100) NULL,
                CONSTRAINT "FK_VisitGuests_Visits_VisitId" FOREIGN KEY ("VisitId") REFERENCES "Visits" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_VisitGuests_VisitId" ON "VisitGuests" ("VisitId");
            ALTER TABLE "Visits" ADD COLUMN IF NOT EXISTS "LinkedBookingId" uuid NULL;

            -- ── WorkTask Assignees ────────────────────────────────
            CREATE TABLE IF NOT EXISTS "WorkTaskAssignees" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "TaskId" uuid NOT NULL,
                "UserId" text NOT NULL,
                "UserName" text NOT NULL,
                "IsOwnerAssigned" boolean NOT NULL DEFAULT TRUE,
                "AssignedById" text NOT NULL DEFAULT '',
                "AssignedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT "FK_WorkTaskAssignees_WorkTasks_TaskId" FOREIGN KEY ("TaskId") REFERENCES "WorkTasks" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkTaskAssignees_TaskId_UserId" ON "WorkTaskAssignees" ("TaskId", "UserId");
            CREATE INDEX IF NOT EXISTS "IX_WorkTaskAssignees_UserId" ON "WorkTaskAssignees" ("UserId");
            ALTER TABLE "WorkTaskAssignees" ADD COLUMN IF NOT EXISTS "AssignedByName" text NOT NULL DEFAULT '';

            -- Migrate existing single-assignee rows into WorkTaskAssignees.
            -- Fresh databases already use WorkTaskAssignees and do not have these legacy columns.
            DO $$
            DECLARE
                has_assignee_name boolean;
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'WorkTasks'
                      AND column_name = 'AssigneeId'
                ) THEN
                    SELECT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'WorkTasks'
                          AND column_name = 'AssigneeName'
                    )
                    INTO has_assignee_name;

                    EXECUTE format(
                        'INSERT INTO "WorkTaskAssignees" ("Id", "TaskId", "UserId", "UserName", "IsOwnerAssigned", "AssignedById", "AssignedAt")
                         SELECT gen_random_uuid(), t."Id", t."AssigneeId", %s, true, t."AuthorId", t."CreatedAt"
                         FROM "WorkTasks" t
                         WHERE t."AssigneeId" IS NOT NULL
                           AND NOT EXISTS (
                               SELECT 1 FROM "WorkTaskAssignees" a WHERE a."TaskId" = t."Id"
                           )',
                        CASE
                            WHEN has_assignee_name THEN 'COALESCE(t."AssigneeName", '''')'
                            ELSE ''''''
                        END
                    );
                END IF;
            END $$;

            -- ── RBAC ──────────────────────────────────────────────
            ALTER TABLE "Users"
                ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT TRUE;

            CREATE TABLE IF NOT EXISTS "Roles" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "Name" character varying(100) NOT NULL,
                "Description" character varying(400) NULL,
                "IsSystem" boolean NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Roles_Name" ON "Roles" ("Name");

            CREATE TABLE IF NOT EXISTS "Permissions" (
                "Code" character varying(80) NOT NULL PRIMARY KEY,
                "Description" character varying(200) NOT NULL,
                "Category" character varying(80) NOT NULL
            );

            CREATE TABLE IF NOT EXISTS "RolePermissions" (
                "RoleId" uuid NOT NULL,
                "PermissionCode" character varying(80) NOT NULL,
                CONSTRAINT "PK_RolePermissions" PRIMARY KEY ("RoleId", "PermissionCode"),
                CONSTRAINT "FK_RolePermissions_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS "UserRoles" (
                "UserId" text NOT NULL,
                "RoleId" uuid NOT NULL,
                CONSTRAINT "PK_UserRoles" PRIMARY KEY ("UserId", "RoleId")
            );
            CREATE INDEX IF NOT EXISTS "IX_UserRoles_UserId" ON "UserRoles" ("UserId");
            """,
            cancellationToken);

        await SyncPermissionsAsync(db, cancellationToken);
        await SeedSystemRolesAsync(db, cancellationToken);
        await SeedSuperAdminAsync(db, config, cancellationToken);
    }

    static async Task SyncPermissionsAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.Permissions.ToDictionaryAsync(p => p.Code, ct);
        foreach (var desc in Shared.Models.Permissions.All)
        {
            if (existing.TryGetValue(desc.Code, out var p))
            {
                if (p.Description != desc.Description || p.Category != desc.Category)
                {
                    p.Description = desc.Description;
                    p.Category    = desc.Category;
                }
            }
            else
            {
                db.Permissions.Add(new Permission
                {
                    Code        = desc.Code,
                    Description = desc.Description,
                    Category    = desc.Category
                });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    static async Task SeedSystemRolesAsync(AppDbContext db, CancellationToken ct)
    {
        var superAdmin = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.SuperAdmin, ct);
        if (superAdmin is null)
        {
            superAdmin = new Role
            {
                Name = SystemRoles.SuperAdmin,
                Description = "Полный доступ ко всем функциям",
                IsSystem = true
            };
            db.Roles.Add(superAdmin);
            await db.SaveChangesAsync(ct);
        }

        // SuperAdmin must have every permission code
        var allCodes = Shared.Models.Permissions.All.Select(p => p.Code).ToHashSet();
        var existingCodes = await db.RolePermissions
            .Where(rp => rp.RoleId == superAdmin.Id)
            .Select(rp => rp.PermissionCode)
            .ToListAsync(ct);

        foreach (var code in allCodes.Except(existingCodes))
            db.RolePermissions.Add(new RolePermission { RoleId = superAdmin.Id, PermissionCode = code });

        var defaultUser = await db.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.User, ct);
        if (defaultUser is null)
        {
            db.Roles.Add(new Role
            {
                Name = SystemRoles.User,
                Description = "Обычный пользователь без административных прав",
                IsSystem = true
            });
        }

        await db.SaveChangesAsync(ct);
    }

    static async Task SeedSuperAdminAsync(AppDbContext db, IConfiguration config, CancellationToken ct)
    {
        var superAdminRole = await db.Roles.FirstAsync(r => r.Name == SystemRoles.SuperAdmin, ct);

        var anySuperAdmin = await db.UserRoles
            .AnyAsync(ur => ur.RoleId == superAdminRole.Id, ct);
        if (anySuperAdmin)
            return;

        var email    = config["Admin:Email"]    ?? "admin@local";
        var userName = config["Admin:UserName"] ?? "admin";
        var password = config["Admin:Password"] ?? "Admin12345!";

        // Reuse existing user with matching email, otherwise create a new one
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = new AppUser
            {
                UserName     = userName,
                Email        = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsActive     = true
            };
            db.Users.Add(user);
        }
        else
        {
            // Reset password to configured value so the operator can sign in
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.IsActive     = true;
        }

        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = superAdminRole.Id });
        await db.SaveChangesAsync(ct);
    }
}
