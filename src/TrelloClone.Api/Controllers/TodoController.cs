using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrelloClone.Api.Data;
using TrelloClone.Api.Services;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class TodoController(AppDbContext db, INotificationService notif) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string CurrentUserName => User.Identity?.Name ?? "Пользователь";

    // GET /api/todo/lists
    [HttpGet("lists")]
    public async Task<IActionResult> GetLists()
    {
        var lists = await db.TodoLists
            .Include(l => l.Items.OrderBy(i => i.Position))
            .Where(l => l.OwnerId == CurrentUserId)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();
        return Ok(lists);
    }

    // POST /api/todo/lists
    [HttpPost("lists")]
    public async Task<IActionResult> CreateList(CreateTodoListRequest req)
    {
        var list = new TodoList { Title = req.Title, OwnerId = CurrentUserId };
        db.TodoLists.Add(list);
        await db.SaveChangesAsync();
        return Ok(list);
    }

    // PUT /api/todo/lists/{id}
    [HttpPut("lists/{id:guid}")]
    public async Task<IActionResult> UpdateList(Guid id, UpdateTodoListRequest req)
    {
        var list = await db.TodoLists.FirstOrDefaultAsync(l => l.Id == id && l.OwnerId == CurrentUserId);
        if (list is null) return NotFound();
        list.Title = req.Title;
        await db.SaveChangesAsync();
        return Ok(list);
    }

    // DELETE /api/todo/lists/{id}
    [HttpDelete("lists/{id:guid}")]
    public async Task<IActionResult> DeleteList(Guid id)
    {
        var list = await db.TodoLists.FirstOrDefaultAsync(l => l.Id == id && l.OwnerId == CurrentUserId);
        if (list is null) return NotFound();
        db.TodoLists.Remove(list);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/todo/lists/{id}/items
    [HttpPost("lists/{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, CreateTodoItemRequest req)
    {
        var list = await db.TodoLists.FirstOrDefaultAsync(l => l.Id == id && l.OwnerId == CurrentUserId);
        if (list is null) return NotFound();
        var count = await db.TodoItems.CountAsync(i => i.TodoListId == id);
        var item = new TodoItem { TodoListId = id, Text = req.Text, DueDate = req.DueDate, Position = count };
        db.TodoItems.Add(item);
        await db.SaveChangesAsync();
        return Ok(item);
    }

    // PUT /api/todo/items/{id}
    [HttpPut("items/{id:guid}")]
    public async Task<IActionResult> UpdateItem(Guid id, UpdateTodoItemRequest req)
    {
        var item = await db.TodoItems.FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();
        if (!await OwnsItemAsync(item)) return Forbid();

        var becameCompleted = !item.IsCompleted && req.IsCompleted;
        item.Text = req.Text; item.IsCompleted = req.IsCompleted; item.DueDate = req.DueDate;
        await SyncLinkedTaskAsync(item, becameCompleted);
        await db.SaveChangesAsync();
        return Ok(item);
    }

    // PUT /api/todo/items/{id}/toggle
    [HttpPut("items/{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id)
    {
        var item = await db.TodoItems.FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();
        if (!await OwnsItemAsync(item)) return Forbid();

        item.IsCompleted = !item.IsCompleted;
        await SyncLinkedTaskAsync(item, item.IsCompleted);
        await db.SaveChangesAsync();
        return Ok(item);
    }

    // DELETE /api/todo/items/{id}
    [HttpDelete("items/{id:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var item = await db.TodoItems.FindAsync(id);
        if (item is null) return NotFound();
        if (!await OwnsItemAsync(item)) return Forbid();
        db.TodoItems.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> OwnsItemAsync(TodoItem item)
        => await db.TodoLists.AnyAsync(l => l.Id == item.TodoListId && l.OwnerId == CurrentUserId);

    private async Task SyncLinkedTaskAsync(TodoItem item, bool becameCompleted)
    {
        if (!becameCompleted || !item.WorkTaskId.HasValue)
            return;

        var task = await db.WorkTasks.FirstOrDefaultAsync(t => t.Id == item.WorkTaskId.Value);
        if (task is null || task.AssigneeId != CurrentUserId || task.Status == WorkTaskStatus.Done)
            return;

        task.Status = WorkTaskStatus.Done;
        task.UpdatedAt = DateTime.UtcNow;

        if (task.AuthorId != CurrentUserId)
        {
            await notif.SendAsync(
                task.AuthorId,
                "Задача завершена",
                $"{CurrentUserName} отметил(а) задачу \"{task.Title}\" выполненной в ToDo.",
                NotificationType.Task,
                $"/tasks/{task.Id}",
                task.Id.ToString());
        }
    }
}
