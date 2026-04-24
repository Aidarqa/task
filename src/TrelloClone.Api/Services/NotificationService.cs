using Microsoft.AspNetCore.SignalR;
using TrelloClone.Api.Data;
using TrelloClone.Api.Hubs;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Services;

public class NotificationService(AppDbContext db, IHubContext<NotificationHub> hub) : INotificationService
{
    public async Task SendAsync(string userId, string title, string? body, NotificationType type,
        string? link = null, string? relatedEntityId = null)
    {
        var n = new Notification
        {
            UserId = userId, Title = title, Body = body,
            NotificationType = type, Link = link, RelatedEntityId = relatedEntityId
        };
        db.Notifications.Add(n);
        await db.SaveChangesAsync();

        var dto = new NotificationDto(n.Id, n.Title, n.Body, n.NotificationType, false, n.CreatedAt, n.Link, n.RelatedEntityId);
        await hub.Clients.Group(userId).SendAsync("Notification", new NotificationEvent(dto));
    }

    public async Task SendToManyAsync(IEnumerable<string> userIds, string title, string? body,
        NotificationType type, string? link = null, string? relatedEntityId = null)
    {
        foreach (var uid in userIds.Distinct())
            await SendAsync(uid, title, body, type, link, relatedEntityId);
    }
}
