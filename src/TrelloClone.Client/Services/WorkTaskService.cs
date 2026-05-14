using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Json;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class WorkTaskService(HttpClient http, LocalizationService l)
{
    public async Task<List<WorkTaskSummary>> GetAllAsync(string? status = null, string? assignee = null, Guid? projectId = null, bool withoutProject = false)
    {
        var url = "api/worktasks";
        var qs = new List<string>();
        if (status is not null) qs.Add($"status={status}");
        if (assignee is not null) qs.Add($"assignee={assignee}");
        if (withoutProject) qs.Add("withoutProject=true");
        if (projectId.HasValue) qs.Add($"projectId={projectId}");
        if (qs.Count > 0) url += "?" + string.Join("&", qs);
        return (await http.GetFromJsonAsync<List<WorkTaskSummary>>(url) ?? [])
            .Select(NormalizeSummary)
            .ToList();
    }

    public async Task<List<WorkTaskSummary>> GetMyAsync()
        => (await http.GetFromJsonAsync<List<WorkTaskSummary>>("api/worktasks/my") ?? [])
            .Select(NormalizeSummary)
            .ToList();

    public async Task<WorkTaskDetailDto?> GetAsync(Guid id)
    {
        var task = await http.GetFromJsonAsync<WorkTaskDetailDto>($"api/worktasks/{id}");
        return task is null ? null : NormalizeDetail(task);
    }

    public async Task<WorkTaskSummary?> CreateAsync(CreateWorkTaskRequest req)
    {
        var normalizedRequest = req with
        {
            StartDate = KyrgyzstanTime.ConvertToUtc(req.StartDate),
            DueDate = KyrgyzstanTime.ConvertToUtc(req.DueDate)
        };

        var r = await http.PostAsJsonAsync("api/worktasks", normalizedRequest);
        r.EnsureSuccessStatusCode();
        var task = await r.Content.ReadFromJsonAsync<WorkTaskSummary>();
        return task is null ? null : NormalizeSummary(task);
    }

    public async Task<WorkTaskSummary?> UpdateAsync(Guid id, UpdateWorkTaskRequest req)
    {
        var normalizedRequest = req with
        {
            StartDate = KyrgyzstanTime.ConvertToUtc(req.StartDate),
            DueDate = KyrgyzstanTime.ConvertToUtc(req.DueDate)
        };

        var r = await http.PutAsJsonAsync($"api/worktasks/{id}", normalizedRequest);
        r.EnsureSuccessStatusCode();
        var task = await r.Content.ReadFromJsonAsync<WorkTaskSummary>();
        return task is null ? null : NormalizeSummary(task);
    }

    public async Task<WorkTaskSummary?> AddAssigneeAsync(Guid taskId, string userId)
    {
        var r = await http.PostAsJsonAsync($"api/worktasks/{taskId}/assignees", new AddAssigneeRequest(userId));
        r.EnsureSuccessStatusCode();
        var task = await r.Content.ReadFromJsonAsync<WorkTaskSummary>();
        return task is null ? null : NormalizeSummary(task);
    }

    public async Task<WorkTaskSummary?> RemoveAssigneeAsync(Guid taskId, string userId)
    {
        var r = await http.DeleteAsync($"api/worktasks/{taskId}/assignees/{userId}");
        r.EnsureSuccessStatusCode();
        var task = await r.Content.ReadFromJsonAsync<WorkTaskSummary>();
        return task is null ? null : NormalizeSummary(task);
    }

    public async Task<WorkTaskSummary?> UpdateStatusAsync(Guid id, WorkTaskStatus status)
    {
        var r = await http.PutAsJsonAsync($"api/worktasks/{id}/status", new UpdateWorkTaskStatusRequest(status));
        r.EnsureSuccessStatusCode();
        var task = await r.Content.ReadFromJsonAsync<WorkTaskSummary>();
        return task is null ? null : NormalizeSummary(task);
    }

    public async Task<WorkTaskSummary?> AcceptAsync(Guid id)
    {
        var r = await http.PostAsync($"api/worktasks/{id}/accept", null);
        r.EnsureSuccessStatusCode();
        var task = await r.Content.ReadFromJsonAsync<WorkTaskSummary>();
        return task is null ? null : NormalizeSummary(task);
    }

    public async Task<TodoItem?> MoveToTodoAsync(Guid id)
    {
        var r = await http.PostAsync($"api/worktasks/{id}/move-to-todo", null);
        r.EnsureSuccessStatusCode();
        var item = await r.Content.ReadFromJsonAsync<TodoItem>();
        return item is null ? null : NormalizeTodoItem(item);
    }

    public async Task DeleteAsync(Guid id)
        => (await http.DeleteAsync($"api/worktasks/{id}")).EnsureSuccessStatusCode();

    public async Task<WorkTaskComment?> AddCommentAsync(Guid taskId, string text)
    {
        var r = await http.PostAsJsonAsync($"api/worktasks/{taskId}/comments", new CreateWorkTaskCommentRequest(text));
        r.EnsureSuccessStatusCode();
        var comment = await r.Content.ReadFromJsonAsync<WorkTaskComment>();
        return comment is null ? null : NormalizeComment(comment);
    }

    public async Task DeleteCommentAsync(Guid taskId, Guid commentId)
        => (await http.DeleteAsync($"api/worktasks/{taskId}/comments/{commentId}")).EnsureSuccessStatusCode();

    public async Task<WorkTaskChecklistItem?> AddChecklistItemAsync(Guid taskId, string text)
    {
        var r = await http.PostAsJsonAsync($"api/worktasks/{taskId}/checklist", new CreateWorkTaskChecklistItemRequest(text));
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<WorkTaskChecklistItem>();
    }

    public async Task ToggleChecklistItemAsync(Guid taskId, Guid itemId)
        => (await http.PutAsync($"api/worktasks/{taskId}/checklist/{itemId}/toggle", null)).EnsureSuccessStatusCode();

    public async Task DeleteChecklistItemAsync(Guid taskId, Guid itemId)
        => (await http.DeleteAsync($"api/worktasks/{taskId}/checklist/{itemId}")).EnsureSuccessStatusCode();

    public async Task<List<FileAttachmentDto>> UploadAttachmentsAsync(Guid taskId, IReadOnlyList<IBrowserFile> files, bool pinToTask)
    {
        foreach (var file in files)
        {
            var validationError = FileUploadRules.Validate(file, l);
            if (validationError is not null)
                throw new InvalidOperationException(validationError);
        }

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(pinToTask.ToString().ToLowerInvariant()), "pinToTask");

        foreach (var file in files)
        {
            var streamContent = new StreamContent(file.OpenReadStream(FileUploadRules.GetMaxReadSize(file)));
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            content.Add(streamContent, "files", file.Name);
        }

        var r = await http.PostAsync($"api/worktasks/{taskId}/attachments", content);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<List<FileAttachmentDto>>() ?? [])
            .Select(NormalizeAttachment)
            .ToList();
    }

    public async Task<FileAttachmentDto?> ToggleAttachmentPinAsync(Guid taskId, Guid attachmentId)
    {
        var r = await http.PutAsync($"api/worktasks/{taskId}/attachments/{attachmentId}/pin", null);
        r.EnsureSuccessStatusCode();
        var attachment = await r.Content.ReadFromJsonAsync<FileAttachmentDto>();
        return attachment is null ? null : NormalizeAttachment(attachment);
    }

    public async Task DeleteAttachmentAsync(Guid taskId, Guid attachmentId)
        => (await http.DeleteAsync($"api/worktasks/{taskId}/attachments/{attachmentId}")).EnsureSuccessStatusCode();

    private static WorkTaskSummary NormalizeSummary(WorkTaskSummary task)
        => task with
        {
            DueDate = KyrgyzstanTime.ConvertFromApi(task.DueDate),
            CreatedAt = KyrgyzstanTime.ConvertFromApi(task.CreatedAt)
        };

    private static WorkTaskDetailDto NormalizeDetail(WorkTaskDetailDto task)
        => task with
        {
            StartDate = KyrgyzstanTime.ConvertFromApi(task.StartDate),
            DueDate = KyrgyzstanTime.ConvertFromApi(task.DueDate),
            CreatedAt = KyrgyzstanTime.ConvertFromApi(task.CreatedAt),
            UpdatedAt = KyrgyzstanTime.ConvertFromApi(task.UpdatedAt),
            Comments = task.Comments.Select(NormalizeComment).ToList(),
            Attachments = task.Attachments.Select(NormalizeAttachment).ToList()
        };

    private static WorkTaskComment NormalizeComment(WorkTaskComment comment)
    {
        comment.CreatedAt = KyrgyzstanTime.ConvertFromApi(comment.CreatedAt);
        return comment;
    }

    private static FileAttachmentDto NormalizeAttachment(FileAttachmentDto attachment)
        => attachment with
        {
            UploadedAt = KyrgyzstanTime.ConvertFromApi(attachment.UploadedAt)
        };

    private static TodoItem NormalizeTodoItem(TodoItem item)
    {
        item.DueDate = KyrgyzstanTime.ConvertFromApi(item.DueDate);
        item.CreatedAt = KyrgyzstanTime.ConvertFromApi(item.CreatedAt);
        return item;
    }
}
