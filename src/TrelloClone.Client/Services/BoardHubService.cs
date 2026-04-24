using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class BoardHubService : IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly ILocalStorageService _localStorage;
    private readonly NavigationManager _nav;

    public event Action<CardMovedEvent>? OnCardMoved;
    public event Action<BoardUpdatedEvent>? OnBoardUpdated;

    public BoardHubService(ILocalStorageService localStorage, NavigationManager nav)
    {
        _localStorage = localStorage;
        _nav = nav;
    }

    public async Task ConnectAsync(Guid boardId)
    {
        if (_hub is not null) await DisposeAsync();

        var token = await _localStorage.GetItemAsStringAsync("authToken");
        token = token?.Trim('"');

        var apiBase = _nav.BaseUri.Contains("localhost:5002")
            ? "https://localhost:5001"
            : _nav.BaseUri.TrimEnd('/');

        _hub = new HubConnectionBuilder()
            .WithUrl($"{apiBase}/hubs/board?access_token={token}")
            .WithAutomaticReconnect()
            .Build();

        _hub.On<CardMovedEvent>("CardMoved", evt => OnCardMoved?.Invoke(evt));
        _hub.On<BoardUpdatedEvent>("BoardUpdated", evt => OnBoardUpdated?.Invoke(evt));

        await _hub.StartAsync();
        await _hub.InvokeAsync("JoinBoard", boardId.ToString());
    }

    public async Task DisconnectAsync(Guid boardId)
    {
        if (_hub is not null)
        {
            await _hub.InvokeAsync("LeaveBoard", boardId.ToString());
            await _hub.StopAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }
}
