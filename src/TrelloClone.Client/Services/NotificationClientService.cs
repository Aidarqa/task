using Blazored.LocalStorage;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class NotificationClientService : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;
    private HubConnection? _hub;

    public event Action<NotificationDto>? OnNotification;
    public event Action? OnCountChanged;
    public event Action? OnPresenceChanged;
    public event Action? OnReconnected;

    public HashSet<string> OnlineUserIds { get; } = [];

    public NotificationClientService(HttpClient http, ILocalStorageService localStorage)
    {
        _http = http;
        _localStorage = localStorage;
    }

    public async Task ConnectAsync()
    {
        if (_hub?.State is HubConnectionState.Connected
                        or HubConnectionState.Connecting
                        or HubConnectionState.Reconnecting) return;

        // Dispose stale disconnected hub before creating a fresh one
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }

        var token = await _localStorage.GetItemAsStringAsync("authToken");
        token = token?.Trim('"');

        // Don't connect without a token — hub requires authorization
        if (string.IsNullOrWhiteSpace(token)) return;

        var apiBase = _http.BaseAddress!.ToString().TrimEnd('/');

        _hub = new HubConnectionBuilder()
            .WithUrl($"{apiBase}/hubs/notifications", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<NotificationEvent>("Notification", evt =>
        {
            OnNotification?.Invoke(NormalizeNotification(evt.Notification));
            OnCountChanged?.Invoke();
        });

        _hub.On<string>("UserOnline", userId =>
        {
            OnlineUserIds.Add(userId);
            OnPresenceChanged?.Invoke();
        });

        _hub.On<string>("UserOffline", userId =>
        {
            OnlineUserIds.Remove(userId);
            OnPresenceChanged?.Invoke();
        });

        _hub.Reconnected += async _ =>
        {
            try
            {
                var fresh = await _http.GetFromJsonAsync<List<string>>("api/presence") ?? [];
                OnlineUserIds.Clear();
                foreach (var id in fresh)
                    OnlineUserIds.Add(id);
            }
            catch
            {
                // Presence data may be stale — clear to avoid showing wrong status
                OnlineUserIds.Clear();
            }
            finally
            {
                OnPresenceChanged?.Invoke();
                // Notify listeners so they can re-fetch missed notifications
                OnReconnected?.Invoke();
            }
        };

        // Apply snapshot BEFORE starting hub so SignalR events flow on top of correct initial state.
        // If we started the hub first, UserOnline/UserOffline events could arrive and then get
        // wiped when we apply the snapshot — causing users to appear offline incorrectly.
        var onlineUsers = await _http.GetFromJsonAsync<List<string>>("api/presence") ?? [];
        OnlineUserIds.Clear();
        foreach (var id in onlineUsers)
            OnlineUserIds.Add(id);

        await _hub.StartAsync();

        OnPresenceChanged?.Invoke();
    }

    public async Task<List<NotificationDto>> GetAllAsync(bool unreadOnly = false, string? type = null)
    {
        var url = $"api/notifications?unreadOnly={unreadOnly}";
        if (type is not null) url += $"&type={type}";
        return (await _http.GetFromJsonAsync<List<NotificationDto>>(url) ?? [])
            .Select(NormalizeNotification)
            .ToList();
    }

    public async Task<int> GetUnreadCountAsync()
        => await _http.GetFromJsonAsync<int>("api/notifications/count");

    public async Task<int> GetChatUnreadCountAsync()
        => await _http.GetFromJsonAsync<int>("api/notifications/chat-count");

    public async Task<int> GetSystemUnreadCountAsync()
        => await _http.GetFromJsonAsync<int>("api/notifications/system-count");

    public async Task MarkReadAsync(Guid id)
    {
        await _http.PutAsync($"api/notifications/{id}/read", null);
        OnCountChanged?.Invoke();
    }

    public async Task MarkAllReadAsync()
    {
        await _http.PutAsync("api/notifications/read-all", null);
        OnCountChanged?.Invoke();
    }

    public async Task DeleteAsync(Guid id)
        => (await _http.DeleteAsync($"api/notifications/{id}")).EnsureSuccessStatusCode();

    public async Task<int> BroadcastAsync(BroadcastNotificationRequest req)
    {
        var r = await _http.PostAsJsonAsync("api/notifications/broadcast", req);
        r.EnsureSuccessStatusCode();
        var result = await r.Content.ReadFromJsonAsync<BroadcastResult>();
        return result?.Sent ?? 0;
    }

    private record BroadcastResult(int Sent);

    // Mark all chat notifications for a specific chat as read (called when opening that chat)
    public async Task MarkChatNotificationsReadAsync(Guid chatId)
    {
        await _http.PutAsync($"api/notifications/mark-chat-read/{chatId}", null);
        OnCountChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }

    private static NotificationDto NormalizeNotification(NotificationDto notification)
        => notification with
        {
            CreatedAt = KyrgyzstanTime.ConvertFromApi(notification.CreatedAt)
        };
}
