using Microsoft.EntityFrameworkCore;
using TrelloClone.Api.Data;

namespace TrelloClone.Api.Services;

public static class SchemaBootstrapper
{
    public static async Task EnsureLatestAsync(AppDbContext db, CancellationToken cancellationToken = default)
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
            """,
            cancellationToken);
    }
}
