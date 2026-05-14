using Blazored.LocalStorage;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class BoardHubService : IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly ILocalStorageService _localStorage;
    private readonly HttpClient _http;
    private Guid? _currentBoardId;

    public event Action<CardMovedEvent>? OnCardMoved;
    public event Action<BoardUpdatedEvent>? OnBoardUpdated;

    public BoardHubService(ILocalStorageService localStorage, HttpClient http)
    {
        _localStorage = localStorage;
        _http = http;
    }

    public async Task ConnectAsync(Guid boardId)
    {
        if (_hub is not null) await DisposeAsync();

        _currentBoardId = boardId;

        var token = await _localStorage.GetItemAsStringAsync("authToken");
        token = token?.Trim('"');

        var apiBase = _http.BaseAddress!.ToString().TrimEnd('/');

        _hub = new HubConnectionBuilder()
            .WithUrl($"{apiBase}/hubs/board", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<CardMovedEvent>("CardMoved", evt => OnCardMoved?.Invoke(evt));
        _hub.On<BoardUpdatedEvent>("BoardUpdated", evt => OnBoardUpdated?.Invoke(evt));

        _hub.Reconnected += async _ =>
        {
            try
            {
                if (_currentBoardId.HasValue)
                    await _hub.InvokeAsync("JoinBoard", _currentBoardId.Value.ToString());
            }
            catch { }
        };

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
