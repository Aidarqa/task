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
        {
            var fromUtc = KyrgyzstanTime.NormalizeUtc(from.Value);
            q = q.Where(e => e.EndTime >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = KyrgyzstanTime.NormalizeUtc(to.Value);
            q = q.Where(e => e.StartTime <= toUtc);
        }

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

        if (ev.OrganizerId != CurrentUserId && !ev.Participants.Any(p => p.UserId == CurrentUserId))
            return Forbid();

        var resources = await db.Resources.ToDictionaryAsync(r => r.Id, r => r.Name);
        return Ok(ToDto(ev, resources));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCalendarEventRequest req)
    {
        var start = KyrgyzstanTime.NormalizeUtc(req.StartTime);
        var end = KyrgyzstanTime.NormalizeUtc(req.EndTime);

        if (IsCreateInPast(start, req.IsAllDay))
            return BadRequest(new { error = "Нельзя создать событие задним числом" });

        if (end <= start)
            return BadRequest(new { error = "Время окончания должно быть позже времени начала" });

        if (req.ResourceId.HasValue && await HasResourceConflictAsync(req.ResourceId.Value, start, end))
            return Conflict(new { error = "Кабинет уже забронирован на это время" });

        var ev = new CalendarEvent
        {
            Title = req.Title,
            Description = req.Description,
            StartTime = start,
            EndTime = end,
            IsAllDay = req.IsAllDay,
            Color = req.Color,
            EventType = req.EventType,
            OrganizerId = CurrentUserId,
            OrganizerName = CurrentUserName,
            ResourceId = req.ResourceId
        };

        ev.Participants = await BuildParticipantsAsync(ev.Id, req.ParticipantIds);

        db.CalendarEvents.Add(ev);
        if (req.ResourceId.HasValue)
            db.Bookings.Add(CreateBooking(ev));

        await db.SaveChangesAsync();

        var participantIds = ev.Participants.Select(p => p.UserId)
            .Where(uid => uid != CurrentUserId);

        var startLocal = KyrgyzstanTime.ConvertFromUtc(ev.StartTime);
        await notif.SendToManyAsync(
            participantIds,
            "Приглашение на событие",
            $"«{ev.Title}» — {startLocal:dd.MM.yyyy HH:mm}",
            NotificationType.Event,
            "/calendar",
            ev.Id.ToString(),
            senderUserId: CurrentUserId);

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

        var start = KyrgyzstanTime.NormalizeUtc(req.StartTime);
        var end = KyrgyzstanTime.NormalizeUtc(req.EndTime);

        if (req.ResourceId.HasValue && await HasResourceConflictAsync(req.ResourceId.Value, start, end, ev.Id))
            return Conflict(new { error = "Кабинет уже забронирован на это время" });

        ev.Title = req.Title;
        ev.Description = req.Description;
        ev.StartTime = start;
        ev.EndTime = end;
        ev.IsAllDay = req.IsAllDay;
        ev.Color = req.Color;
        ev.EventType = req.EventType;
        ev.ResourceId = req.ResourceId;

        var currentParticipants = await db.EventParticipants
            .Where(p => p.EventId == ev.Id)
            .ToListAsync();
        db.EventParticipants.RemoveRange(currentParticipants);

        var newParticipants = await BuildParticipantsAsync(ev.Id, req.ParticipantIds);
        if (newParticipants.Count > 0)
            await db.EventParticipants.AddRangeAsync(newParticipants);

        await SyncBookingAsync(ev);
        await db.SaveChangesAsync();

        var participantIds = newParticipants.Select(p => p.UserId)
            .Where(uid => uid != CurrentUserId);

        var startLocalUpd = KyrgyzstanTime.ConvertFromUtc(ev.StartTime);
        await notif.SendToManyAsync(
            participantIds,
            "Событие изменено",
            $"«{ev.Title}» — {startLocalUpd:dd.MM.yyyy HH:mm}",
            NotificationType.Event,
            "/calendar",
            ev.Id.ToString(),
            senderUserId: CurrentUserId);

        var resources = await db.Resources.ToDictionaryAsync(r => r.Id, r => r.Name);
        ev.Participants = newParticipants;
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

        await db.Bookings
            .Where(b => b.EventId == id)
            .ExecuteDeleteAsync();

        db.CalendarEvents.Remove(ev);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<List<EventParticipant>> BuildParticipantsAsync(Guid eventId, string[]? participantIds)
    {
        if (participantIds is not { Length: > 0 })
            return [];

        var ids = participantIds
            .Where(id => id != CurrentUserId)
            .Distinct()
            .ToArray();

        var users = await db.Users.Where(u => ids.Contains(u.Id)).ToListAsync();
        return users.Select(u => new EventParticipant
        {
            EventId = eventId,
            UserId = u.Id,
            UserName = u.UserName ?? u.Email ?? u.Id
        }).ToList();
    }

    private static bool IsCreateInPast(DateTime startUtc, bool isAllDay)
        => isAllDay
            ? KyrgyzstanTime.ConvertFromUtc(startUtc).Date < KyrgyzstanTime.Today
            : startUtc < DateTime.UtcNow;

    private async Task<bool> HasResourceConflictAsync(Guid resourceId, DateTime start, DateTime end, Guid? eventId = null)
        => await db.Bookings.AnyAsync(b =>
            b.ResourceId == resourceId &&
            b.Status != BookingStatus.Cancelled &&
            b.StartTime < end &&
            b.EndTime > start &&
            (!eventId.HasValue || b.EventId != eventId.Value));

    private Booking CreateBooking(CalendarEvent ev)
        => new()
        {
            ResourceId = ev.ResourceId!.Value,
            BookedById = CurrentUserId,
            BookedByName = CurrentUserName,
            StartTime = ev.StartTime,
            EndTime = ev.EndTime,
            Title = ev.Title,
            EventId = ev.Id
        };

    private async Task SyncBookingAsync(CalendarEvent ev)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.EventId == ev.Id);
        if (!ev.ResourceId.HasValue)
        {
            if (booking is not null)
                db.Bookings.Remove(booking);

            return;
        }

        if (booking is null)
        {
            db.Bookings.Add(CreateBooking(ev));
            return;
        }

        booking.ResourceId = ev.ResourceId.Value;
        booking.StartTime = ev.StartTime;
        booking.EndTime = ev.EndTime;
        booking.Title = ev.Title;
        booking.Status = BookingStatus.Confirmed;
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
