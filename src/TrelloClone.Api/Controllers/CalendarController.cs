using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrelloClone.Api.Data;
using TrelloClone.Api.Services;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class CalendarController(AppDbContext db, INotificationService notif) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string CurrentUserName => User.Identity!.Name!;

    [HttpGet]
    public async Task<IActionResult> GetEvents([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var q = db.CalendarEvents
            .Include(e => e.Participants)
            .Where(e => e.OrganizerId == CurrentUserId ||
                        e.Participants.Any(p => p.UserId == CurrentUserId));

        if (from.HasValue)
            q = q.Where(e => e.EndTime >= from.Value);

        if (to.HasValue)
            q = q.Where(e => e.StartTime <= to.Value);

        var events = await q.OrderBy(e => e.StartTime).ToListAsync();
        var resources = await db.Resources.ToDictionaryAsync(r => r.Id, r => r.Name);

        return Ok(events.Select(e => ToDto(e, resources)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var ev = await db.CalendarEvents
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null)
            return NotFound();

        var resources = await db.Resources.ToDictionaryAsync(r => r.Id, r => r.Name);
        return Ok(ToDto(ev, resources));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCalendarEventRequest req)
    {
        var ev = new CalendarEvent
        {
            Title = req.Title,
            Description = req.Description,
            StartTime = req.StartTime,
            EndTime = req.EndTime,
            IsAllDay = req.IsAllDay,
            Color = req.Color,
            EventType = req.EventType,
            OrganizerId = CurrentUserId,
            OrganizerName = CurrentUserName,
            ResourceId = req.ResourceId
        };

        if (req.ParticipantIds is { Length: > 0 })
        {
            var users = await db.Users.Where(u => req.ParticipantIds.Contains(u.Id)).ToListAsync();
            ev.Participants = users.Select(u => new EventParticipant
            {
                EventId = ev.Id,
                UserId = u.Id,
                UserName = u.UserName
            }).ToList();
        }

        db.CalendarEvents.Add(ev);
        await db.SaveChangesAsync();

        var participantIds = ev.Participants.Select(p => p.UserId)
            .Where(uid => uid != CurrentUserId);

        await notif.SendToManyAsync(
            participantIds,
            "Приглашение на событие",
            $"«{ev.Title}» — {ev.StartTime:dd.MM.yyyy HH:mm}",
            NotificationType.Event,
            "/calendar",
            ev.Id.ToString());

        var resources = await db.Resources.ToDictionaryAsync(r => r.Id, r => r.Name);
        return Ok(ToDto(ev, resources));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCalendarEventRequest req)
    {
        var ev = await db.CalendarEvents
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null)
            return NotFound();

        if (ev.OrganizerId != CurrentUserId)
            return Forbid();

        ev.Title = req.Title;
        ev.Description = req.Description;
        ev.StartTime = req.StartTime;
        ev.EndTime = req.EndTime;
        ev.IsAllDay = req.IsAllDay;
        ev.Color = req.Color;
        ev.EventType = req.EventType;
        ev.ResourceId = req.ResourceId;
        await db.SaveChangesAsync();

        var participantIds = ev.Participants.Select(p => p.UserId)
            .Where(uid => uid != CurrentUserId);

        await notif.SendToManyAsync(
            participantIds,
            "Событие изменено",
            $"«{ev.Title}» — {ev.StartTime:dd.MM.yyyy HH:mm}",
            NotificationType.Event,
            "/calendar",
            ev.Id.ToString());

        var resources = await db.Resources.ToDictionaryAsync(r => r.Id, r => r.Name);
        return Ok(ToDto(ev, resources));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ev = await db.CalendarEvents.FindAsync(id);
        if (ev is null)
            return NotFound();

        if (ev.OrganizerId != CurrentUserId)
            return Forbid();

        db.CalendarEvents.Remove(ev);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static CalendarEventDto ToDto(CalendarEvent e, Dictionary<Guid, string> resources) => new(
        e.Id,
        e.Title,
        e.Description,
        e.StartTime,
        e.EndTime,
        e.IsAllDay,
        e.Color,
        e.EventType,
        e.OrganizerId,
        e.OrganizerName,
        e.ResourceId,
        e.ResourceId.HasValue && resources.TryGetValue(e.ResourceId.Value, out var resourceName) ? resourceName : null,
        e.Participants);
}
