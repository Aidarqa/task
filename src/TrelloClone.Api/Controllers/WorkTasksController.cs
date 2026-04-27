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
            .Where(t => t.ParentTaskId == null && (t.AssigneeId == CurrentUserId || t.AuthorId == CurrentUserId));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<WorkTaskStatus>(status, out var parsedStatus))
            query = query.Where(t => t.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(assignee))
            query = query.Where(t => t.AssigneeId == assignee);

        if (withoutProject)
        {
            query = query.Where(t => t.ProjectId == null);
        }
        else if (projectId.HasValue)
        {
            if (!await CanAccessProjectAsync(projectId.Value))
                return Forbid();

            query = query.Where(t => t.ProjectId == projectId);
        }

        var tasks = await query
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync();

        return Ok(tasks.Select(ToSummary));
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var tasks = await BaseTaskQuery()
            .Where(t => t.ParentTaskId == null && t.AssigneeId == CurrentUserId)
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

        if (task is null)
            return NotFound();

        if (!CanAccessTask(task))
            return Forbid();

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
            AssigneeId = req.AssigneeId,
            StartDate = KyrgyzstanTime.NormalizeUtc(req.StartDate),
            DueDate = KyrgyzstanTime.NormalizeUtc(req.DueDate),
            ParentTaskId = req.ParentTaskId,
            ProjectId = req.ProjectId,
            AuthorId = CurrentUserId,
            AuthorName = CurrentUserName,
            TagsJson = req.Tags is { Length: > 0 } ? string.Join(",", req.Tags) : null
        };

        if (!string.IsNullOrWhiteSpace(req.AssigneeId))
        {
            var assignee = await db.Users.FindAsync(req.AssigneeId);
            task.AssigneeName = assignee?.UserName;
        }

        db.WorkTasks.Add(task);
        await db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(task.AssigneeId) && task.AssigneeId != CurrentUserId)
        {
            await notif.SendAsync(
                task.AssigneeId,
                "Назначена новая задача",
                $"Задача \"{task.Title}\" назначена пользователем {CurrentUserName}.",
                NotificationType.Task,
                $"/tasks/{task.Id}",
                task.Id.ToString());
        }

        return Ok(ToSummary(task));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateWorkTaskRequest req)
    {
        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return NotFound();

        if (task.AuthorId != CurrentUserId)
            return Forbid();

        var previousAssigneeId = task.AssigneeId;

        task.Title = req.Title;
        task.Description = req.Description;
        task.Status = req.Status;
        task.Priority = req.Priority;
        task.AssigneeId = req.AssigneeId;
        task.StartDate = KyrgyzstanTime.NormalizeUtc(req.StartDate);
        task.DueDate = KyrgyzstanTime.NormalizeUtc(req.DueDate);
        task.TagsJson = req.Tags is { Length: > 0 } ? string.Join(",", req.Tags) : null;
        task.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(req.AssigneeId))
        {
            var assignee = await db.Users.FindAsync(req.AssigneeId);
            task.AssigneeName = assignee?.UserName;
        }
        else
        {
            task.AssigneeName = null;
        }

        await db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(task.AssigneeId)
            && task.AssigneeId != previousAssigneeId
            && task.AssigneeId != CurrentUserId)
        {
            await notif.SendAsync(
                task.AssigneeId,
                "Назначена новая задача",
                $"Задача \"{task.Title}\" была обновлена и назначена вам.",
                NotificationType.Task,
                $"/tasks/{task.Id}",
                task.Id.ToString());
        }

        return Ok(ToSummary(task));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateWorkTaskStatusRequest req)
    {
        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return NotFound();

        if (!CanAccessTask(task))
            return Forbid();

        var previousStatus = task.Status;
        if (previousStatus == req.Status)
            return Ok(ToSummary(task));

        task.Status = req.Status;
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (CurrentUserId == task.AssigneeId && task.AuthorId != CurrentUserId)
        {
            if (req.Status == WorkTaskStatus.Done)
            {
                await notif.SendAsync(
                    task.AuthorId,
                    "Задача завершена",
                    $"Исполнитель отметил задачу \"{task.Title}\" как выполненную.",
                    NotificationType.Task,
                    $"/tasks/{task.Id}",
                    task.Id.ToString());
            }
            else
            {
                await notif.SendAsync(
                    task.AuthorId,
                    "Статус задачи обновлён",
                    $"По задаче \"{task.Title}\" установлен статус {req.Status}.",
                    NotificationType.Task,
                    $"/tasks/{task.Id}",
                    task.Id.ToString());
            }
        }

        return Ok(ToSummary(task));
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id)
    {
        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return NotFound();

        if (task.AssigneeId != CurrentUserId)
            return Forbid();

        task.Status = WorkTaskStatus.InProgress;
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (task.AuthorId != CurrentUserId)
        {
            await notif.SendAsync(
                task.AuthorId,
                "Задача принята в работу",
                $"{CurrentUserName} принял(а) задачу \"{task.Title}\" в работу.",
                NotificationType.Task,
                $"/tasks/{task.Id}",
                task.Id.ToString());
        }

        return Ok(ToSummary(task));
    }

    [HttpPost("{id:guid}/move-to-todo")]
    public async Task<IActionResult> MoveToTodo(Guid id)
    {
        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return NotFound();

        if (task.AssigneeId != CurrentUserId)
            return Forbid();

        var todoList = await db.TodoLists
            .OrderBy(l => l.CreatedAt)
            .FirstOrDefaultAsync(l => l.OwnerId == CurrentUserId);

        if (todoList is null)
        {
            todoList = new TodoList
            {
                Title = "Рабочие задачи",
                OwnerId = CurrentUserId
            };
            db.TodoLists.Add(todoList);
            await db.SaveChangesAsync();
        }

        var existingItem = await db.TodoItems.FirstOrDefaultAsync(i =>
            i.TodoListId == todoList.Id &&
            (i.WorkTaskId == task.Id || (i.WorkTaskId == null && i.Text == task.Title && i.DueDate == task.DueDate)));

        if (existingItem is not null)
        {
            if (existingItem.WorkTaskId != task.Id)
            {
                existingItem.WorkTaskId = task.Id;
                await db.SaveChangesAsync();
            }

            return Ok(existingItem);
        }

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
        if (task is null)
            return NotFound();

        if (task.AuthorId != CurrentUserId)
            return Forbid();

        db.WorkTasks.Remove(task);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, CreateWorkTaskCommentRequest req)
    {
        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return NotFound();

        if (!CanAccessTask(task))
            return Forbid();

        var comment = new WorkTaskComment
        {
            TaskId = id,
            Text = req.Text,
            AuthorId = CurrentUserId,
            AuthorName = CurrentUserName
        };

        db.WorkTaskComments.Add(comment);
        await db.SaveChangesAsync();

        var recipients = new[] { task.AuthorId, task.AssigneeId }
            .Where(userId => !string.IsNullOrWhiteSpace(userId) && userId != CurrentUserId)
            .Distinct()!
            .Cast<string>();

        var preview = req.Text.Length > 80 ? req.Text[..80] + "..." : req.Text;
        await notif.SendToManyAsync(
            recipients,
            "Новый комментарий к задаче",
            $"\"{task.Title}\": {preview}",
            NotificationType.Task,
            $"/tasks/{task.Id}",
            task.Id.ToString());

        return Ok(comment);
    }

    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId)
    {
        var comment = await db.WorkTaskComments.FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == id);
        if (comment is null)
            return NotFound();

        if (comment.AuthorId != CurrentUserId)
            return Forbid();

        db.WorkTaskComments.Remove(comment);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/checklist")]
    public async Task<IActionResult> AddChecklist(Guid id, CreateWorkTaskChecklistItemRequest req)
    {
        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return NotFound();

        if (!CanAccessTask(task))
            return Forbid();

        var count = await db.WorkTaskChecklists.CountAsync(c => c.TaskId == id);
        var item = new WorkTaskChecklistItem
        {
            TaskId = id,
            Text = req.Text,
            Position = count
        };

        db.WorkTaskChecklists.Add(item);
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpPut("{id:guid}/checklist/{itemId:guid}/toggle")]
    public async Task<IActionResult> ToggleChecklist(Guid id, Guid itemId)
    {
        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return NotFound();

        if (!CanAccessTask(task))
            return Forbid();

        var item = await db.WorkTaskChecklists.FirstOrDefaultAsync(c => c.Id == itemId && c.TaskId == id);
        if (item is null)
            return NotFound();

        item.IsChecked = !item.IsChecked;
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:guid}/checklist/{itemId:guid}")]
    public async Task<IActionResult> DeleteChecklist(Guid id, Guid itemId)
    {
        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return NotFound();

        if (!CanAccessTask(task))
            return Forbid();

        var item = await db.WorkTaskChecklists.FirstOrDefaultAsync(c => c.Id == itemId && c.TaskId == id);
        if (item is null)
            return NotFound();

        db.WorkTaskChecklists.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(MaxFileSize * 5)]
    public async Task<IActionResult> AddAttachments(Guid id, [FromForm] List<IFormFile> files, [FromForm] bool pinToTask = false, CancellationToken cancellationToken = default)
    {
        var task = await db.WorkTasks.Include(t => t.Attachments).FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (task is null)
            return NotFound();

        if (!CanAccessTask(task))
            return Forbid();

        if (files.Count == 0)
            return BadRequest("No files uploaded.");

        var created = new List<FileAttachment>();

        foreach (var file in files.Where(f => f.Length > 0))
        {
            var validationError = FileUploadPolicy.Validate(file);
            if (validationError is not null)
                return BadRequest(validationError);

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

        var recipients = new[] { task.AuthorId, task.AssigneeId }
            .Where(userId => !string.IsNullOrWhiteSpace(userId) && userId != CurrentUserId)
            .Distinct()!
            .Cast<string>();

        await notif.SendToManyAsync(
            recipients,
            "Добавлены файлы к задаче",
            $"{CurrentUserName} добавил(а) {created.Count} файл(ов) к задаче \"{task.Title}\".",
            NotificationType.Task,
            $"/tasks/{task.Id}",
            task.Id.ToString());

        return Ok(created.Select(ToAttachmentDto));
    }

    [HttpPut("{id:guid}/attachments/{attachmentId:guid}/pin")]
    public async Task<IActionResult> ToggleAttachmentPin(Guid id, Guid attachmentId)
    {
        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
            return NotFound();

        if (!CanAccessTask(task))
            return Forbid();

        var attachment = await db.FileAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.WorkTaskId == id);
        if (attachment is null)
            return NotFound();

        attachment.IsPinned = !attachment.IsPinned;
        await db.SaveChangesAsync();
        return Ok(ToAttachmentDto(attachment));
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (task is null)
            return NotFound();

        if (!CanAccessTask(task))
            return Forbid();

        var attachment = await db.FileAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.WorkTaskId == id, cancellationToken);
        if (attachment is null)
            return NotFound();

        if (attachment.UploadedById != CurrentUserId && task.AuthorId != CurrentUserId)
            return Forbid();

        db.FileAttachments.Remove(attachment);
        await db.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(attachment.StoragePath, cancellationToken);

        return NoContent();
    }

    private IQueryable<WorkTask> BaseTaskQuery()
        => db.WorkTasks
            .Include(t => t.SubTasks)
            .Include(t => t.Comments)
            .Include(t => t.Checklist);

    private bool CanAccessTask(WorkTask task)
        => task.AuthorId == CurrentUserId || task.AssigneeId == CurrentUserId;

    private async Task<bool> CanAccessProjectAsync(Guid projectId)
        => await db.Projects.AnyAsync(p => p.Id == projectId
            && (p.Visibility == ProjectVisibility.AllUsers
                || p.OwnerId == CurrentUserId
                || p.Members.Any(m => m.UserId == CurrentUserId)));

    private FileAttachmentDto ToAttachmentDto(FileAttachment attachment)
        => new(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.Size,
            attachment.UploadedById,
            attachment.UploadedByName,
            attachment.UploadedAt,
            attachment.IsPinned,
            $"{Request.Scheme}://{Request.Host}/api/files/{attachment.Id}");

    private WorkTaskDetailDto ToDetail(WorkTask task)
        => new(
            task.Id,
            task.Title,
            task.Description,
            task.Status,
            task.Priority,
            task.AuthorId,
            task.AuthorName,
            task.AssigneeId,
            task.AssigneeName,
            task.StartDate,
            task.DueDate,
            task.CreatedAt,
            task.UpdatedAt,
            task.ParentTaskId,
            task.ProjectId,
            task.TagsJson,
            task.Comments.OrderByDescending(c => c.CreatedAt).ToList(),
            task.Checklist.OrderBy(c => c.Position).ToList(),
            task.Attachments
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.UploadedAt)
                .Select(ToAttachmentDto)
                .ToList());

    private static WorkTaskSummary ToSummary(WorkTask task)
        => new(
            task.Id,
            task.Title,
            task.Status,
            task.Priority,
            task.AuthorId,
            task.AuthorName,
            task.AssigneeId,
            task.AssigneeName,
            task.DueDate,
            task.CreatedAt,
            task.SubTasks.Count,
            task.Comments.Count,
            task.Checklist.Count,
            task.Checklist.Count(c => c.IsChecked),
            task.ProjectId,
            null);
}
