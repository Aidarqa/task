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
            presence.UserConnected(userId);
            // Always broadcast — covers the race where old connection hasn't closed yet
            // when the user reconnects (e.g. re-login without full page reload).
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
