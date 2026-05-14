using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrelloClone.Api.Data;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Hubs;

[Authorize]
public class BoardHub(AppDbContext db) : Hub
{
    public async Task JoinBoard(string boardId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !Guid.TryParse(boardId, out var parsedBoardId))
            throw new HubException("Board access denied.");

        var isMember = await db.BoardMembers.AnyAsync(m => m.BoardId == parsedBoardId && m.UserId == userId);
        if (!isMember)
            throw new HubException("Board access denied.");

        await Groups.AddToGroupAsync(Context.ConnectionId, boardId);
    }

    public async Task LeaveBoard(string boardId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, boardId);

    public async Task NotifyCardMoved(string boardId, CardMovedEvent evt)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !Guid.TryParse(boardId, out var parsedBoardId))
            throw new HubException("Board access denied.");

        var isMember = await db.BoardMembers.AnyAsync(m => m.BoardId == parsedBoardId && m.UserId == userId);
        if (!isMember)
            throw new HubException("Board access denied.");

        await Clients.OthersInGroup(boardId).SendAsync("CardMoved", evt);
    }

    public async Task NotifyBoardUpdated(string boardId, BoardUpdatedEvent evt)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !Guid.TryParse(boardId, out var parsedBoardId))
            throw new HubException("Board access denied.");

        var isMember = await db.BoardMembers.AnyAsync(m => m.BoardId == parsedBoardId && m.UserId == userId);
        if (!isMember)
            throw new HubException("Board access denied.");

        await Clients.OthersInGroup(boardId).SendAsync("BoardUpdated", evt);
    }
}
