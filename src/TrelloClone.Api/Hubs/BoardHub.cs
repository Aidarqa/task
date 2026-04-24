using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Hubs;

[Authorize]
public class BoardHub : Hub
{
    public async Task JoinBoard(string boardId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, boardId);
    }

    public async Task LeaveBoard(string boardId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, boardId);
    }

    public async Task NotifyCardMoved(string boardId, CardMovedEvent evt)
    {
        await Clients.OthersInGroup(boardId).SendAsync("CardMoved", evt);
    }

    public async Task NotifyBoardUpdated(string boardId, BoardUpdatedEvent evt)
    {
        await Clients.OthersInGroup(boardId).SendAsync("BoardUpdated", evt);
    }
}
