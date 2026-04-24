using System.Collections.Concurrent;

namespace TrelloClone.Api.Services;

public class PresenceService : IPresenceService
{
    // userId → active connection count (multiple tabs/windows per user)
    private readonly ConcurrentDictionary<string, int> _connections = new();

    public bool UserConnected(string userId)
    {
        var count = _connections.AddOrUpdate(userId, 1, (_, c) => c + 1);
        return count == 1;
    }

    public bool UserDisconnected(string userId)
    {
        while (true)
        {
            if (!_connections.TryGetValue(userId, out var count))
                return false;

            if (count <= 1)
                return _connections.TryRemove(new KeyValuePair<string, int>(userId, count));

            if (_connections.TryUpdate(userId, count - 1, count))
                return false;
            // Another thread changed the value concurrently — retry.
        }
    }

    public IReadOnlySet<string> GetOnlineUsers() => _connections.Keys.ToHashSet();

    public bool IsOnline(string userId) => _connections.ContainsKey(userId);
}
