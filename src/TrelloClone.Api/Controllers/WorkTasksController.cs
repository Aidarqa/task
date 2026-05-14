using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrelloClone.Api.Data;
using TrelloClone.Api.Services;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class WorkTasksController(
    AppDbContext db,
    INotificationService notif,
    IFileStorageService storage) : ControllerBase
{
    private const long MaxFileSize = FileUploadPolicy.MaxDocumentFileSize;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string CurrentUserName => User.Identity!.Name!;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? assignee, [FromQuery] Guid? projectId, [FromQuery] bool withoutProject = false)
    {
        var query = BaseTaskQuery()
            .Where(t => t.ParentTaskId == null
                && (t.AuthorId == CurrentUserId
                    || t.Assignees.Any(a => a.UserId == CurrentUserId)));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<WorkTaskStatus>(status, out var parsedStatus))
            query = query.Where(t => t.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(assignee))
            query = query.Where(t => t.Assignees.Any(a => a.UserId == assignee));

        if (withoutProject)
            query = query.Where(t => t.ProjectId == null);
        else if (projectId.HasValue)
        {
            if (!await CanAccessProjectAsync(projectId.Value))
                return Forbid();
            query = query.Where(t => t.ProjectId == projectId);
        }

        var tasks = await query.OrderByDescending(t => t.UpdatedAt).ToListAsync();
        return Ok(tasks.Select(ToSummary));
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var tasks = await BaseTaskQuery()
            .Where(t => t.ParentTaskId == null && t.Assignees.Any(a => a.UserId == CurrentUserId))
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync();
        return Ok(tasks.Select(ToSummary));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var task = await BaseTaskQuery()
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task is null) return NotFound();
        if (!CanAccessTask(task)) return Forbid();
        return Ok(ToDetail(task));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWorkTaskRequest req)
    {
        if (req.ProjectId.HasValue && !await CanAccessProjectAsync(req.ProjectId.Value))
            return Forbid();

        var task = new WorkTask
        {
            Title = req.Title,
            Description = req.Description,
            Priority = req.Priority,
            StartDate = KyrgyzstanTime.NormalizeUtc(req.StartDate),
            DueDate = KyrgyzstanTime.NormalizeUtc(req.DueDate),
            ParentTaskId = req.ParentTaskId,
            ProjectId = req.ProjectId,
            AuthorId = CurrentUserId,
            AuthorName = CurrentUserName,
            TagsJson = req.Tags is { Length: > 0 } ? string.Join(",", req.Tags) : null
        };

        db.WorkTasks.Add(task);
        await db.SaveChangesAsync();

        var newAssignees = await AddAssigneesAsync(task.Id, req.AssigneeIds ?? [], isOwnerAssigned: true);
        await db.SaveChangesAsync();

        // Notify all assignees except the author
        foreach (var a in newAssignees.Where(a => a.UserId != CurrentUserId))
        {
            await notif.SendAsync(
                a.UserId,
                "Назначена новая задача",
                $"Задача «{task.Title}» назначена вам пользователем {CurrentUserName}.",
                NotificationType.Task,
                $"/tasks/{task.Id}",
                task.Id.ToString(),
                senderUserId: CurrentUserId);
        }

        task.Assignees = newAssignees;
        return Ok(ToSummary(task));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateWorkTaskRequest req)
    {
        var task = await BaseTaskQuery().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();
        if (task.AuthorId != CurrentUserId) return Forbid();

        var previousAssigneeIds = task.Assignees.Select(a => a.UserId).ToHashSet();

        task.Title = req.Title;
        task.Description = req.Description;
        task.Status = req.Status;
        task.Priority = req.Priority;
        task.StartDate = KyrgyzstanTime.NormalizeUtc(req.StartDate);
        task.DueDate = KyrgyzstanTime.NormalizeUtc(req.DueDate);
        task.TagsJson = req.Tags is { Length: > 0 } ? string.Join(",", req.Tags) : null;
        task.UpdatedAt = DateTime.UtcNow;

        // Replace owner-assigned assignees with the new list
        var ownerAssigned = task.Assignees.Where(a => a.IsOwnerAssigned).ToList();
        db.WorkTaskAssignees.RemoveRange(ownerAssigned);
        await db.SaveChangesAsync();

        var newAssignees = await AddAssigneesAsync(task.Id, req.AssigneeIds ?? [], isOwnerAssigned: true);
        await db.SaveChangesAsync();

        // Also set AssignedByName for the newly added owner-assigned entries
        foreach (var a in newAssignees)
            a.AssignedByName = CurrentUserName;

        // Notify newly added assignees
        foreach (var a in newAssignees.Where(a => !previousAssigneeIds.Contains(a.UserId) && a.UserId != CurrentUserId))
        {
            await notif.SendAsync(
                a.UserId,
                "Назначена новая задача",
                $"Задача «{task.Title}» назначена вам пользователем {CurrentUserName}.",
                NotificationType.Task,
                $"/tasks/{task.Id}",
                task.Id.ToString(),
                senderUserId: CurrentUserId);
        }

        task.Assignees = await db.WorkTaskAssignees.Where(a => a.TaskId == id).ToListAsync();
        return Ok(ToSummary(task));
    }

    // POST /api/worktasks/{id}/assignees — любой исполнитель или автор может добавить
    [HttpPost("{id:guid}/assignees")]
    public async Task<IActionResult> AddAssignee(Guid id, AddAssigneeRequest req)
    {
        var task = await BaseTaskQuery().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();
        if (!CanAccessTask(task)) return Forbid();

        if (task.Assignees.Any(a => a.UserId == req.UserId))
            return Ok(ToSummary(task)); // already assigned — idempotent

        var user = await db.Users.FindAsync(req.UserId);
        if (user is null) return NotFound("User not found");

        var isOwnerAssigned = task.AuthorId == CurrentUserId;
        var assignee = new WorkTaskAssignee
        {
            TaskId = id,
            UserId = req.UserId,
            UserName = user.UserName ?? req.UserId,
            IsOwnerAssigned = isOwnerAssigned,
            AssignedById = CurrentUserId
        };

        db.WorkTaskAssignees.Add(assignee);
        await db.SaveChangesAsync();

        if (req.UserId != CurrentUserId)
        {
            await notif.SendAsync(
                req.UserId,
                "Вас назначили исполнителем",
                $"Задача «{task.Title}» — {CurrentUserName} добавил(а) вас исполнителем.",
                NotificationType.Task,
                $"/tasks/{task.Id}",
                task.Id.ToString(),
                senderUserId: CurrentUserId);
        }

        task.Assignees = await db.WorkTaskAssignees.Where(a => a.TaskId == id).ToListAsync();
        return Ok(ToSummary(task));
    }

    // DELETE /api/worktasks/{id}/assignees/{userId}
    [HttpDelete("{id:guid}/assignees/{userId}")]
    public async Task<IActionResult> RemoveAssignee(Guid id, string userId)
    {
        var task = await BaseTaskQuery().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();
        if (!CanAccessTask(task)) return Forbid();

        var assignee = task.Assignees.FirstOrDefault(a => a.UserId == userId);
        if (assignee is null) return NotFound();

        // Owner-assigned entries can only be removed by the task author
        if (assignee.IsOwnerAssigned && task.AuthorId != CurrentUserId)
            return Forbid();

        db.WorkTaskAssignees.Remove(assignee);
        await db.SaveChangesAsync();

        task.Assignees = await db.WorkTaskAssignees.Where(a => a.TaskId == id).ToListAsync();
        return Ok(ToSummary(task));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateWorkTaskStatusRequest req)
    {
        var task = await BaseTaskQuery().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();
        if (!CanAccessTask(task)) return Forbid();

        if (task.Status == req.Status) return Ok(ToSummary(task));

        task.Status = req.Status;
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (task.Assignees.Any(a => a.UserId == CurrentUserId) && task.AuthorId != CurrentUserId)
        {
            var body = req.Status == WorkTaskStatus.Done
                ? $"Исполнитель отметил задачу «{task.Title}» как выполненную."
                : $"По задаче «{task.Title}» установлен статус {req.Status}.";
            var title = req.Status == WorkTaskStatus.Done ? "Задача завершена" : "Статус задачи обновлён";

            await notif.SendAsync(task.AuthorId, title, body,
                NotificationType.Task, $"/tasks/{task.Id}", task.Id.ToString(), senderUserId: CurrentUserId);
        }

        return Ok(ToSummary(task));
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id)
    {
        var task = await BaseTaskQuery().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();
        if (!task.Assignees.Any(a => a.UserId == CurrentUserId)) return Forbid();

        task.Status = WorkTaskStatus.InProgress;
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (task.AuthorId != CurrentUserId)
        {
            await notif.SendAsync(task.AuthorId,
                "Задача принята в работу",
                $"{CurrentUserName} принял(а) задачу «{task.Title}» в работу.",
                NotificationType.Task, $"/tasks/{task.Id}", task.Id.ToString(), senderUserId: CurrentUserId);
        }

        return Ok(ToSummary(task));
    }

    [HttpPost("{id:guid}/move-to-todo")]
    public async Task<IActionResult> MoveToTodo(Guid id)
    {
        var task = await BaseTaskQuery().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();
        if (!task.Assignees.Any(a => a.UserId == CurrentUserId)) return Forbid();

        var todoList = await db.TodoLists
            .OrderBy(l => l.CreatedAt)
            .FirstOrDefaultAsync(l => l.OwnerId == CurrentUserId);

        if (todoList is null)
        {
            todoList = new TodoList { Title = "Рабочие задачи", OwnerId = CurrentUserId };
            db.TodoLists.Add(todoList);
            await db.SaveChangesAsync();
        }

        var existingItem = await db.TodoItems.FirstOrDefaultAsync(i =>
            i.TodoListId == todoList.Id && i.WorkTaskId == task.Id);

        if (existingItem is not null)
            return Ok(existingItem);

        var position = await db.TodoItems.CountAsync(i => i.TodoListId == todoList.Id);
        var item = new TodoItem
        {
            TodoListId = todoList.Id,
            Text = task.Title,
            DueDate = task.DueDate,
            Position = position,
            WorkTaskId = task.Id
        };

        db.TodoItems.Add(item);
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();
        if (task.AuthorId != CurrentUserId) return Forbid();

        db.WorkTasks.Remove(task);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, CreateWorkTaskCommentRequest req)
    {
        var task = await BaseTaskQuery().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();
        if (!CanAccessTask(task)) return Forbid();

        var comment = new WorkTaskComment
        {
            TaskId = id, Text = req.Text,
            AuthorId = CurrentUserId, AuthorName = CurrentUserName
        };

        db.WorkTaskComments.Add(comment);
        await db.SaveChangesAsync();

        var recipients = task.Assignees.Select(a => a.UserId)
            .Append(task.AuthorId)
            .Distinct()
            .Where(uid => uid != CurrentUserId);

        var preview = req.Text.Length > 80 ? req.Text[..80] + "..." : req.Text;
        await notif.SendToManyAsync(recipients,
            "Новый комментарий к задаче",
            $"«{task.Title}»: {preview}",
            NotificationType.Task, $"/tasks/{task.Id}", task.Id.ToString(), senderUserId: CurrentUserId);

        return Ok(comment);
    }

    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId)
    {
        var comment = await db.WorkTaskComments.FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == id);
        if (comment is null) return NotFound();
        if (comment.AuthorId != CurrentUserId) return Forbid();

        db.WorkTaskComments.Remove(comment);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/checklist")]
    public async Task<IActionResult> AddChecklist(Guid id, CreateWorkTaskChecklistItemRequest req)
    {
        var task = await BaseTaskQuery().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();
        if (!CanAccessTask(task)) return Forbid();

        var count = await db.WorkTaskChecklists.CountAsync(c => c.TaskId == id);
        var item = new WorkTaskChecklistItem { TaskId = id, Text = req.Text, Position = count };
        db.WorkTaskChecklists.Add(item);
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpPut("{id:guid}/checklist/{itemId:guid}/toggle")]
    public async Task<IActionResult> ToggleChecklist(Guid id, Guid itemId)
    {
        var task = await BaseTaskQuery().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();
        if (!CanAccessTask(task)) return Forbid();

        var item = await db.WorkTaskChecklists.FirstOrDefaultAsync(c => c.Id == itemId && c.TaskId == id);
        if (item is null) return NotFound();

        item.IsChecked = !item.IsChecked;
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:guid}/checklist/{itemId:guid}")]
    public async Task<IActionResult> DeleteChecklist(Guid id, Guid itemId)
    {
        var task = await BaseTaskQuery().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();
        if (!CanAccessTask(task)) return Forbid();

        var item = await db.WorkTaskChecklists.FirstOrDefaultAsync(c => c.Id == itemId && c.TaskId == id);
        if (item is null) return NotFound();

        db.WorkTaskChecklists.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(MaxFileSize * 5)]
    public async Task<IActionResult> AddAttachments(Guid id, [FromForm] List<IFormFile> files,
        [FromForm] bool pinToTask = false, CancellationToken cancellationToken = default)
    {
        var task = await BaseTaskQuery().Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (task is null) return NotFound();
        if (!CanAccessTask(task)) return Forbid();
        if (files.Count == 0) return BadRequest("No files uploaded.");

        var created = new List<FileAttachment>();
        foreach (var file in files.Where(f => f.Length > 0))
        {
            var validationError = FileUploadPolicy.Validate(file);
            if (validationError is not null) return BadRequest(validationError);

            await using var stream = file.OpenReadStream();
            var storagePath = await storage.SaveAsync(stream, file.FileName, $"tasks/{task.Id}", cancellationToken);
            created.Add(new FileAttachment
            {
                FileName = file.FileName,
                StoragePath = storagePath,
                ContentType = file.ContentType ?? "application/octet-stream",
                Size = file.Length,
                UploadedById = CurrentUserId,
                UploadedByName = CurrentUserName,
                WorkTaskId = task.Id,
                IsPinned = pinToTask
            });
        }

        db.FileAttachments.AddRange(created);
        await db.SaveChangesAsync(cancellationToken);

        var recipients = task.Assignees.Select(a => a.UserId)
            .Append(task.AuthorId)
            .Distinct()
            .Where(uid => uid != CurrentUserId);

        await notif.SendToManyAsync(recipients,
            "Добавлены файлы к задаче",
            $"{CurrentUserName} добавил(а) {created.Count} файл(ов) к задаче «{task.Title}».",
            NotificationType.Task, $"/tasks/{task.Id}", task.Id.ToString(), senderUserId: CurrentUserId);

        return Ok(created.Select(ToAttachmentDto));
    }

    [HttpPut("{id:guid}/attachments/{attachmentId:guid}/pin")]
    public async Task<IActionResult> ToggleAttachmentPin(Guid id, Guid attachmentId)
    {
        var task = await BaseTaskQuery().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();
        if (!CanAccessTask(task)) return Forbid();

        var attachment = await db.FileAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.WorkTaskId == id);
        if (attachment is null) return NotFound();

        attachment.IsPinned = !attachment.IsPinned;
        await db.SaveChangesAsync();
        return Ok(ToAttachmentDto(attachment));
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var task = await BaseTaskQuery().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (task is null) return NotFound();
        if (!CanAccessTask(task)) return Forbid();

        var attachment = await db.FileAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.WorkTaskId == id, cancellationToken);
        if (attachment is null) return NotFound();

        if (attachment.UploadedById != CurrentUserId && task.AuthorId != CurrentUserId)
            return Forbid();

        db.FileAttachments.Remove(attachment);
        await db.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(attachment.StoragePath, cancellationToken);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────

    private IQueryable<WorkTask> BaseTaskQuery()
        => db.WorkTasks
            .Include(t => t.Assignees)
            .Include(t => t.SubTasks)
            .Include(t => t.Comments)
            .Include(t => t.Checklist);

    private bool CanAccessTask(WorkTask task)
        => task.AuthorId == CurrentUserId || task.Assignees.Any(a => a.UserId == CurrentUserId);

    private async Task<bool> CanAccessProjectAsync(Guid projectId)
        => await db.Projects.AnyAsync(p => p.Id == projectId
            && (p.Visibility == ProjectVisibility.AllUsers
                || p.OwnerId == CurrentUserId
                || p.Members.Any(m => m.UserId == CurrentUserId)));

    private async Task<List<WorkTaskAssignee>> AddAssigneesAsync(
        Guid taskId, string[] userIds, bool isOwnerAssigned)
    {
        var result = new List<WorkTaskAssignee>();
        foreach (var uid in userIds.Distinct())
        {
            var user = await db.Users.FindAsync(uid);
            if (user is null) continue;

            var existing = await db.WorkTaskAssignees.FirstOrDefaultAsync(a => a.TaskId == taskId && a.UserId == uid);
            if (existing is not null) { result.Add(existing); continue; }

            var entry = new WorkTaskAssignee
            {
                TaskId = taskId,
                UserId = uid,
                UserName = user.UserName ?? uid,
                IsOwnerAssigned = isOwnerAssigned,
                AssignedById = CurrentUserId,
                AssignedByName = CurrentUserName
            };
            db.WorkTaskAssignees.Add(entry);
            result.Add(entry);
        }
        return result;
    }

    private FileAttachmentDto ToAttachmentDto(FileAttachment a)
        => new(a.Id, a.FileName, a.ContentType, a.Size, a.UploadedById, a.UploadedByName,
               a.UploadedAt, a.IsPinned, $"{Request.Scheme}://{Request.Host}/api/files/{a.Id}");

    private WorkTaskDetailDto ToDetail(WorkTask task)
        => new(task.Id, task.Title, task.Description, task.Status, task.Priority,
               task.AuthorId, task.AuthorName,
               task.Assignees.Select(a => new WorkTaskAssigneeDto(a.UserId, a.UserName, a.IsOwnerAssigned, a.AssignedById, a.AssignedByName)).ToList(),
               task.StartDate, task.DueDate, task.CreatedAt, task.UpdatedAt,
               task.ParentTaskId, task.ProjectId, task.TagsJson,
               task.Comments.OrderByDescending(c => c.CreatedAt).ToList(),
               task.Checklist.OrderBy(c => c.Position).ToList(),
               task.Attachments.OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.UploadedAt)
                   .Select(ToAttachmentDto).ToList());

    private static WorkTaskSummary ToSummary(WorkTask task)
        => new(task.Id, task.Title, task.Status, task.Priority,
               task.AuthorId, task.AuthorName,
               task.Assignees.Select(a => new WorkTaskAssigneeDto(a.UserId, a.UserName, a.IsOwnerAssigned, a.AssignedById, a.AssignedByName)).ToList(),
               task.DueDate, task.CreatedAt,
               task.SubTasks.Count, task.Comments.Count,
               task.Checklist.Count, task.Checklist.Count(c => c.IsChecked),
               task.ProjectId, null);
}
