using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Services;

public interface INotificationService
{
    Task SendAsync(string userId, string title, string? body, NotificationType type,
        string? link = null, string? relatedEntityId = null, string? senderUserId = null);

    Task SendToManyAsync(IEnumerable<string> userIds, string title, string? body,
        NotificationType type, string? link = null, string? relatedEntityId = null,
        string? senderUserId = null);
}
