using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrelloClone.Api.Data;
using TrelloClone.Api.Services;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class DashboardController(AppDbContext db) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET /api/dashboard
    [HttpGet]
    public async Task<IActionResult> GetStats()
    {
        var nowUtc = DateTime.UtcNow;
        var todayStartUtc = nowUtc.Date;
        var todayEndUtc = todayStartUtc.AddDays(1);
        var nowLocal = KyrgyzstanTime.Now;
        var todayStartLocal = KyrgyzstanTime.Today;
        var todayEndLocal = todayStartLocal.AddDays(1);

        var accessibleTasks = db.WorkTasks.Where(t =>
            t.ParentTaskId == null &&
            (t.AssigneeId == CurrentUserId || t.AuthorId == CurrentUserId));

        var totalTasks = await accessibleTasks.CountAsync(t => t.Status != WorkTaskStatus.Cancelled);
        var myTasks = await db.WorkTasks.CountAsync(t => t.ParentTaskId == null &&
            t.Status != WorkTaskStatus.Cancelled && t.Status != WorkTaskStatus.Done &&
            t.AssigneeId == CurrentUserId);
        var overdue = await accessibleTasks.CountAsync(t =>
            t.Status != WorkTaskStatus.Done && t.Status != WorkTaskStatus.Cancelled &&
            t.DueDate < nowUtc);
        var dueToday = await accessibleTasks.CountAsync(t =>
            t.Status != WorkTaskStatus.Done && t.Status != WorkTaskStatus.Cancelled &&
            t.DueDate >= todayStartUtc && t.DueDate < todayEndUtc);
        var unreadNotifications = await db.Notifications.CountAsync(n => n.UserId == CurrentUserId && !n.IsRead);
        var upcomingEvents = await db.CalendarEvents.CountAsync(e =>
            (e.OrganizerId == CurrentUserId || e.Participants.Any(p => p.UserId == CurrentUserId)) &&
            e.StartTime >= nowLocal && e.StartTime < nowLocal.AddDays(7));

        var recentTasks = await db.WorkTasks
            .Include(t => t.SubTasks).Include(t => t.Comments).Include(t => t.Checklist)
            .Where(t => t.ParentTaskId == null && (t.AssigneeId == CurrentUserId || t.AuthorId == CurrentUserId))
            .OrderByDescending(t => t.UpdatedAt)
            .Take(15)
            .ToListAsync();

        var todayEvents = await db.CalendarEvents
            .Include(e => e.Participants)
            .Where(e => (e.OrganizerId == CurrentUserId || e.Participants.Any(p => p.UserId == CurrentUserId)) &&
                        e.StartTime >= todayStartLocal && e.StartTime < todayEndLocal)
            .OrderBy(e => e.StartTime)
            .ToListAsync();

        var resources = await db.Resources.ToDictionaryAsync(r => r.Id, r => r.Name);

        return Ok(new DashboardStats(
            totalTasks, myTasks, overdue, dueToday,
            unreadNotifications, upcomingEvents,
            recentTasks.Select(t => new WorkTaskSummary(
                t.Id, t.Title, t.Status, t.Priority,
                t.AuthorId, t.AuthorName, t.AssigneeId, t.AssigneeName,
                t.DueDate, t.CreatedAt, t.SubTasks.Count, t.Comments.Count,
                t.Checklist.Count, t.Checklist.Count(c => c.IsChecked), t.ProjectId, null)).ToList(),
            todayEvents.Select(e => new CalendarEventDto(
                e.Id, e.Title, e.Description, e.StartTime, e.EndTime, e.IsAllDay,
                e.Color, e.EventType, e.OrganizerId, e.OrganizerName,
                e.ResourceId, e.ResourceId.HasValue && resources.TryGetValue(e.ResourceId.Value, out var rn) ? rn : null,
                e.Participants)).ToList()
        ));
    }
}
