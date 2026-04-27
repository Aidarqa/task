using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TrelloClone.Api.Data;

namespace TrelloClone.Api.Hubs;

[Authorize]
public class ChatHub(AppDbContext db) : Hub
{
    public async Task JoinChat(string chatId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !Guid.TryParse(chatId, out var parsedChatId))
            throw new HubException("Chat access denied.");

        var isMember = await db.ChatMembers.AnyAsync(m => m.ChatId == parsedChatId && m.UserId == userId);
        if (!isMember)
            throw new HubException("Chat access denied.");

        await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
    }

    public async Task LeaveChat(string chatId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);
}
