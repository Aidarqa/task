using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrelloClone.Api.Data;
using TrelloClone.Api.Services;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class BookingsController(AppDbContext db, INotificationService notif) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string CurrentUserName => User.Identity!.Name!;

    [HttpGet("resources")]
    public async Task<IActionResult> GetResources()
        => Ok(await db.Resources.Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync());

    [HttpPost("resources")]
    public async Task<IActionResult> CreateResource(CreateResourceRequest req)
    {
        var resource = new Resource
        {
            Name = req.Name,
            ResourceType = req.ResourceType,
            Description = req.Description,
            Capacity = req.Capacity,
            Location = req.Location
        };

        db.Resources.Add(resource);
        await db.SaveChangesAsync();
        return Ok(resource);
    }

    [HttpPut("resources/{id:guid}")]
    public async Task<IActionResult> UpdateResource(Guid id, UpdateResourceRequest req)
    {
        var resource = await db.Resources.FindAsync(id);
        if (resource is null)
            return NotFound();

        resource.Name = req.Name;
        resource.Description = req.Description;
        resource.Capacity = req.Capacity;
        resource.Location = req.Location;
        resource.IsActive = req.IsActive;
        await db.SaveChangesAsync();
        return Ok(resource);
    }

    [HttpDelete("resources/{id:guid}")]
    public async Task<IActionResult> DeleteResource(Guid id)
    {
        var resource = await db.Resources.FindAsync(id);
        if (resource is null)
            return NotFound();

        db.Resources.Remove(resource);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetBookings([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? resourceId)
    {
        var q = db.Bookings.Include(b => b.Resource).AsQueryable();

        if (resourceId.HasValue)
            q = q.Where(b => b.ResourceId == resourceId.Value);

        if (from.HasValue)
        {
            var fromUtc = KyrgyzstanTime.NormalizeUtc(from.Value);
            q = q.Where(b => b.EndTime >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = KyrgyzstanTime.NormalizeUtc(to.Value);
            q = q.Where(b => b.StartTime <= toUtc);
        }

        var list = await q.OrderBy(b => b.StartTime).ToListAsync();
        var eventIds = list
            .Where(b => b.EventId.HasValue)
            .Select(b => b.EventId!.Value)
            .Distinct()
            .ToList();

        var participantsByEventId = eventIds.Count == 0
            ? new Dictionary<Guid, List<EventParticipant>>()
            : await db.CalendarEvents
                .Include(e => e.Participants)
                .Where(e => eventIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.Participants);

        return Ok(list.Select(booking => ToDto(booking, participantsByEventId)));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateBookingRequest req)
    {
        var conflict = await db.Bookings.AnyAsync(b =>
            b.ResourceId == req.ResourceId &&
            b.Status != BookingStatus.Cancelled &&
            b.StartTime < KyrgyzstanTime.NormalizeUtc(req.EndTime) &&
            b.EndTime > KyrgyzstanTime.NormalizeUtc(req.StartTime));

        if (conflict)
            return Conflict(new { error = "Кабинет уже забронирован на это время" });

        var booking = new Booking
        {
            ResourceId = req.ResourceId,
            BookedById = CurrentUserId,
            BookedByName = CurrentUserName,
            StartTime = KyrgyzstanTime.NormalizeUtc(req.StartTime),
            EndTime = KyrgyzstanTime.NormalizeUtc(req.EndTime),
            Title = req.Title,
            EventId = req.EventId
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        await db.Entry(booking).Reference(b => b.Resource).LoadAsync();

        var resourceName = booking.Resource?.Name ?? "кабинет";
        var startLocal = KyrgyzstanTime.ConvertFromUtc(booking.StartTime);
        var endLocal = KyrgyzstanTime.ConvertFromUtc(booking.EndTime);
        await notif.SendAsync(
            CurrentUserId,
            "Бронирование подтверждено",
            $"{resourceName} — {startLocal:dd.MM.yyyy HH:mm}–{endLocal:dd.MM.yyyy HH:mm}",
            NotificationType.Booking,
            "/bookings",
            booking.Id.ToString(),
            senderUserId: CurrentUserId);

        return Ok(ToDto(booking));
    }

    [HttpPut("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var booking = await db.Bookings
            .Include(b => b.Resource)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null)
            return NotFound();

        if (booking.BookedById != CurrentUserId)
            return Forbid();

        booking.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync();

        var resourceName = booking.Resource?.Name ?? "кабинет";
        var startLocalCnl = KyrgyzstanTime.ConvertFromUtc(booking.StartTime);
        var endLocalCnl = KyrgyzstanTime.ConvertFromUtc(booking.EndTime);
        await notif.SendAsync(
            CurrentUserId,
            "Бронирование отменено",
            $"{resourceName} — {startLocalCnl:dd.MM.yyyy HH:mm}–{endLocalCnl:dd.MM.yyyy HH:mm}",
            NotificationType.Booking,
            "/bookings",
            booking.Id.ToString(),
            senderUserId: CurrentUserId);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var booking = await db.Bookings.FindAsync(id);
        if (booking is null)
            return NotFound();

        var isAdmin = User.HasClaim("perm", Permissions.AdminAccess);
        if (booking.BookedById != CurrentUserId && !isAdmin)
            return Forbid();

        db.Bookings.Remove(booking);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static BookingDto ToDto(
        Booking b,
        IReadOnlyDictionary<Guid, List<EventParticipant>>? participantsByEventId = null) => new(
        b.Id,
        b.ResourceId,
        b.Resource?.Name ?? "",
        b.Resource?.Location ?? "",
        b.BookedById,
        b.BookedByName,
        b.StartTime,
        b.EndTime,
        b.Title,
        b.Status,
        b.CreatedAt,
        b.EventId,
        b.EventId.HasValue && participantsByEventId?.TryGetValue(b.EventId.Value, out var participants) == true
            ? participants
            : []);
}
