using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrelloClone.Api.Data;
using TrelloClone.Api.Services;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class VisitsController(AppDbContext db, INotificationService notif) : ControllerBase
{
    private string CurrentUserId   => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string CurrentUserName => User.Identity!.Name!;

    // GET /api/visits/check-room?resourceId=...&from=...&to=...&excludeVisitId=...
    [HttpGet("check-room")]
    public async Task<IActionResult> CheckRoom(
        [FromQuery] Guid resourceId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? excludeVisitId = null)
    {
        var fromUtc = KyrgyzstanTime.NormalizeUtc(from);
        var toUtc   = KyrgyzstanTime.NormalizeUtc(to);

        var bookingConflict = await db.Bookings.AnyAsync(b =>
            b.ResourceId == resourceId
            && b.Status == BookingStatus.Confirmed
            && b.StartTime < toUtc
            && b.EndTime   > fromUtc);

        return Ok(new { available = !bookingConflict });
    }

    // GET /api/visits  — visits I created OR where I'm approver
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var uid = CurrentUserId;
        var visits = await db.Visits
            .Include(v => v.Guests)
            .Where(v => v.HostUserId == uid || v.ApproverId == uid)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();

        return Ok(visits.Select(ToDto));
    }

    // GET /api/visits/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var v = await db.Visits.Include(v => v.Guests).FirstOrDefaultAsync(v => v.Id == id);
        if (v is null) return NotFound();
        if (!CanAccess(v)) return Forbid();
        return Ok(ToDto(v));
    }

    // POST /api/visits
    [HttpPost]
    public async Task<IActionResult> Create(CreateVisitRequest req)
    {
        // Generate number: ВИЗ-YYYY-XXXX
        var year = DateTime.UtcNow.Year;
        var count = await db.Visits.CountAsync(v => v.CreatedAt.Year == year) + 1;
        var number = $"ВИЗ-{year}-{count:D4}";

        // Resolve resource name
        string? resourceName = null;
        if (req.ResourceId.HasValue)
        {
            var res = await db.Resources.FindAsync(req.ResourceId.Value);
            resourceName = res?.Name;
        }

        // Resolve approver name
        string? approverName = null;
        if (!string.IsNullOrWhiteSpace(req.ApproverId))
        {
            var approver = await db.Users.FindAsync(req.ApproverId);
            approverName = approver?.UserName;
        }

        var visit = new Visit
        {
            Number = number,
            Purpose = req.Purpose,
            PlannedArrival = KyrgyzstanTime.NormalizeUtc(req.PlannedArrival),
            PlannedDeparture = KyrgyzstanTime.NormalizeUtc(req.PlannedDeparture),
            HostUserId = CurrentUserId,
            HostUserName = CurrentUserName,
            ApproverId = req.ApproverId,
            ApproverName = approverName,
            ResourceId = req.ResourceId,
            ResourceName = resourceName,
            Guests = (req.Guests ?? []).Select(g => new VisitGuest
            {
                FullName = g.FullName,
                Organization = g.Organization,
                Phone = g.Phone,
                DocumentType = g.DocumentType,
                DocumentNumber = g.DocumentNumber
            }).ToList()
        };

        db.Visits.Add(visit);
        await db.SaveChangesAsync();

        // Notify approver
        if (!string.IsNullOrWhiteSpace(req.ApproverId) && req.ApproverId != CurrentUserId)
        {
            await notif.SendAsync(
                req.ApproverId,
                "Новая заявка на посещение",
                $"{CurrentUserName} создал(а) заявку {number} — требуется согласование.",
                NotificationType.Task,
                $"/visits/{visit.Id}",
                visit.Id.ToString(),
                senderUserId: CurrentUserId);
        }

        return Ok(ToDto(visit));
    }

    // PUT /api/visits/{id}/approve
    [HttpPut("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, ApproveVisitRequest req)
    {
        var v = await db.Visits.Include(v => v.Guests).FirstOrDefaultAsync(v => v.Id == id);
        if (v is null) return NotFound();
        if (v.ApproverId != CurrentUserId) return Forbid();
        if (v.Status != VisitStatus.Pending) return BadRequest("Заявка уже обработана.");

        // If a resource is selected — check availability and create booking
        if (v.ResourceId.HasValue)
        {
            var conflict = await db.Bookings.AnyAsync(b =>
                b.ResourceId == v.ResourceId.Value
                && b.Status == BookingStatus.Confirmed
                && b.StartTime < v.PlannedDeparture
                && b.EndTime   > v.PlannedArrival);

            if (conflict)
                return BadRequest("Переговорная занята в выбранное время. Измените время или выберите другую переговорную.");

            var booking = new Booking
            {
                ResourceId  = v.ResourceId.Value,
                BookedById   = v.HostUserId,
                BookedByName = v.HostUserName,
                StartTime    = v.PlannedArrival,
                EndTime      = v.PlannedDeparture,
                Title        = $"Визит {v.Number}: {v.Purpose}",
                Status       = BookingStatus.Confirmed
            };
            db.Bookings.Add(booking);
            v.LinkedBookingId = booking.Id;
        }

        v.Status = VisitStatus.Approved;
        v.ApproverComment = req.Comment;
        v.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await notif.SendAsync(
            v.HostUserId,
            "Заявка на посещение одобрена",
            $"Заявка {v.Number} одобрена {CurrentUserName}." +
            (v.ResourceId.HasValue ? $" Переговорная забронирована." : ""),
            NotificationType.Task,
            $"/visits/{v.Id}",
            v.Id.ToString(),
            senderUserId: CurrentUserId);

        return Ok(ToDto(v));
    }

    // PUT /api/visits/{id}/reject
    [HttpPut("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, RejectVisitRequest req)
    {
        var v = await db.Visits.Include(v => v.Guests).FirstOrDefaultAsync(v => v.Id == id);
        if (v is null) return NotFound();
        if (v.ApproverId != CurrentUserId) return Forbid();
        if (v.Status != VisitStatus.Pending) return BadRequest("Заявка уже обработана.");

        v.Status = VisitStatus.Rejected;
        v.ApproverComment = req.Comment;
        v.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await notif.SendAsync(
            v.HostUserId,
            "Заявка на посещение отклонена",
            $"Заявка {v.Number} отклонена {CurrentUserName}. Причина: {req.Comment}",
            NotificationType.Task,
            $"/visits/{v.Id}",
            v.Id.ToString(),
            senderUserId: CurrentUserId);

        return Ok(ToDto(v));
    }

    // PUT /api/visits/{id} — edit (only host, only Pending)
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CreateVisitRequest req)
    {
        var v = await db.Visits.Include(v => v.Guests).FirstOrDefaultAsync(v => v.Id == id);
        if (v is null) return NotFound();
        if (v.HostUserId != CurrentUserId) return Forbid();
        if (v.Status != VisitStatus.Pending)
            return BadRequest("Редактировать можно только заявку со статусом «На согласовании».");

        string? resourceName = null;
        if (req.ResourceId.HasValue)
        {
            var res = await db.Resources.FindAsync(req.ResourceId.Value);
            resourceName = res?.Name;
        }

        string? approverName = null;
        if (!string.IsNullOrWhiteSpace(req.ApproverId))
        {
            var approver = await db.Users.FindAsync(req.ApproverId);
            approverName = approver?.UserName;
        }

        v.Purpose = req.Purpose;
        v.PlannedArrival   = KyrgyzstanTime.NormalizeUtc(req.PlannedArrival);
        v.PlannedDeparture = KyrgyzstanTime.NormalizeUtc(req.PlannedDeparture);
        v.ApproverId   = req.ApproverId;
        v.ApproverName = approverName;
        v.ResourceId   = req.ResourceId;
        v.ResourceName = resourceName;

        db.VisitGuests.RemoveRange(v.Guests);
        v.Guests = (req.Guests ?? []).Select(g => new VisitGuest
        {
            VisitId        = v.Id,
            FullName       = g.FullName,
            Organization   = g.Organization,
            Phone          = g.Phone,
            DocumentType   = g.DocumentType,
            DocumentNumber = g.DocumentNumber
        }).ToList();

        await db.SaveChangesAsync();
        return Ok(ToDto(v));
    }

    // DELETE /api/visits/{id} — hard delete (only host)
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var v = await db.Visits.Include(v => v.Guests).FirstOrDefaultAsync(v => v.Id == id);
        if (v is null) return NotFound();
        if (v.HostUserId != CurrentUserId) return Forbid();

        if (v.LinkedBookingId.HasValue)
        {
            var booking = await db.Bookings.FindAsync(v.LinkedBookingId.Value);
            if (booking is not null) db.Bookings.Remove(booking);
        }

        db.Visits.Remove(v);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private bool CanAccess(Visit v) => v.HostUserId == CurrentUserId || v.ApproverId == CurrentUserId;

    private static VisitDto ToDto(Visit v) => new(
        v.Id, v.Number, v.Status, v.Purpose,
        v.PlannedArrival, v.PlannedDeparture,
        v.HostUserId, v.HostUserName,
        v.ApproverId, v.ApproverName, v.ApproverComment, v.ApprovedAt,
        v.ResourceId, v.ResourceName,
        v.CreatedAt,
        v.Guests.Select(g => new VisitGuestDto(
            g.Id, g.FullName, g.Organization, g.Phone, g.DocumentType, g.DocumentNumber)).ToList(),
        v.LinkedBookingId);
}
