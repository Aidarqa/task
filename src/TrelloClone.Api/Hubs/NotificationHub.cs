using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TrelloClone.Api.Services;

namespace TrelloClone.Api.Hubs;

[Authorize]
public class NotificationHub(IPresenceService presence) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            var isFirst = presence.UserConnected(userId);
            // Broadcast UserOnline only on the first connection so that opening a second
            // tab doesn't fire a spurious re-render on every other client.
            // The snapshot endpoint (api/presence) handles clients that connect after this event.
            if (isFirst)
                await Clients.Others.SendAsync("UserOnline", userId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null && presence.UserDisconnected(userId))
            await Clients.Others.SendAsync("UserOffline", userId);
        await base.OnDisconnectedAsync(exception);
    }
}
