using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrelloClone.Api.Data;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class NotificationsController(AppDbContext db) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET /api/notifications?unreadOnly=false&type=Task
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool unreadOnly = false, [FromQuery] string? type = null)
    {
        var q = db.Notifications.Where(n => n.UserId == CurrentUserId);
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        if (type is not null && Enum.TryParse<NotificationType>(type, out var t))
            q = q.Where(n => n.NotificationType == t);
        var list = await q.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();
        return Ok(list.Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.NotificationType,
            n.IsRead, n.CreatedAt, n.Link, n.RelatedEntityId, n.SenderUserId)));
    }

    // GET /api/notifications/count
    [HttpGet("count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await db.Notifications.CountAsync(n => n.UserId == CurrentUserId && !n.IsRead);
        return Ok(count);
    }

    // GET /api/notifications/chat-count
    [HttpGet("chat-count")]
    public async Task<IActionResult> GetChatUnreadCount()
    {
        var count = await db.Notifications.CountAsync(n =>
            n.UserId == CurrentUserId && !n.IsRead && n.NotificationType == NotificationType.Chat);
        return Ok(count);
    }

    // PUT /api/notifications/{id}/read
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var n = await db.Notifications.FindAsync(id);
        if (n is null || n.UserId != CurrentUserId) return NotFound();
        n.IsRead = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // PUT /api/notifications/read-all
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await db.Notifications
            .Where(n => n.UserId == CurrentUserId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return NoContent();
    }

    // PUT /api/notifications/mark-chat-read/{chatId}
    [HttpPut("mark-chat-read/{chatId:guid}")]
    public async Task<IActionResult> MarkChatRead(Guid chatId)
    {
        await db.Notifications
            .Where(n => n.UserId == CurrentUserId && !n.IsRead
                     && n.NotificationType == NotificationType.Chat
                     && n.RelatedEntityId == chatId.ToString())
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return NoContent();
    }

    // DELETE /api/notifications/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var n = await db.Notifications.FindAsync(id);
        if (n is null || n.UserId != CurrentUserId) return NotFound();
        db.Notifications.Remove(n);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
