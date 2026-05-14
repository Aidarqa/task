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

        var token = await _localStorage.GetItemAsStringAsync("authToken");
        token = token?.Trim('"');

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
                OnPresenceChanged?.Invoke();
            }
            catch { }
        };

        // Fetch presence snapshot before starting hub to avoid race with UserOnline/UserOffline events
        var onlineUsers = await _http.GetFromJsonAsync<List<string>>("api/presence") ?? [];

        await _hub.StartAsync();

        OnlineUserIds.Clear();
        foreach (var id in onlineUsers)
            OnlineUserIds.Add(id);

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
