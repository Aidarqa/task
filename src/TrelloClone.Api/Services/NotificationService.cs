using Microsoft.AspNetCore.SignalR;
using TrelloClone.Api.Data;
using TrelloClone.Api.Hubs;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Services;

public class NotificationService(AppDbContext db, IHubContext<NotificationHub> hub) : INotificationService
{
    public async Task SendAsync(string userId, string title, string? body, NotificationType type,
        string? link = null, string? relatedEntityId = null, string? senderUserId = null)
    {
        var n = new Notification
        {
            UserId = userId, Title = title, Body = body,
            NotificationType = type, Link = link, RelatedEntityId = relatedEntityId,
            SenderUserId = senderUserId
        };
        db.Notifications.Add(n);
        await db.SaveChangesAsync();

        var dto = new NotificationDto(n.Id, n.Title, n.Body, n.NotificationType, false,
            n.CreatedAt, n.Link, n.RelatedEntityId, n.SenderUserId);
        await hub.Clients.Group(userId).SendAsync("Notification", new NotificationEvent(dto));
    }

    public async Task SendToManyAsync(IEnumerable<string> userIds, string title, string? body,
        NotificationType type, string? link = null, string? relatedEntityId = null,
        string? senderUserId = null)
    {
        var distinctIds = userIds
            .Where(uid => !string.IsNullOrWhiteSpace(uid))
            .Distinct()
            .ToList();

        if (distinctIds.Count == 0) return;

        // Batch insert — one SaveChangesAsync instead of N
        var notifications = distinctIds.Select(uid => new Notification
        {
            UserId = uid, Title = title, Body = body,
            NotificationType = type, Link = link,
            RelatedEntityId = relatedEntityId, SenderUserId = senderUserId
        }).ToList();

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync();

        // Broadcast via SignalR after all are persisted
        foreach (var n in notifications)
        {
            var dto = new NotificationDto(n.Id, n.Title, n.Body, n.NotificationType, false,
                n.CreatedAt, n.Link, n.RelatedEntityId, n.SenderUserId);
            await hub.Clients.Group(n.UserId).SendAsync("Notification", new NotificationEvent(dto));
        }
    }
}
