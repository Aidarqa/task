namespace TrelloClone.Api.Services;

public interface IPresenceService
{
    /// <summary>Returns true if this is the user's first connection (just came online).</summary>
    bool UserConnected(string userId);

    /// <summary>Returns true if this was the user's last connection (just went offline).</summary>
    bool UserDisconnected(string userId);

    IReadOnlySet<string> GetOnlineUsers();
    bool IsOnline(string userId);
}
