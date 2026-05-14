using Microsoft.AspNetCore.Components.Forms;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class ChatClientService : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;
    private readonly NavigationManager _nav;
    private readonly LocalizationService _l;
    private HubConnection? _hub;
    private Guid? _currentChatId;

    public event Action<NewMessageEvent>? OnNewMessage;
    public event Action<ChatReadEvent>? OnChatRead;
    public event Action<ChatTypingEvent>? OnTyping;
    public event Action<MessageDeletedEvent>? OnMessageDeleted;

    public ChatClientService(HttpClient http, ILocalStorageService localStorage, NavigationManager nav, LocalizationService l)
    {
        _http = http;
        _localStorage = localStorage;
        _nav = nav;
        _l = l;
    }

    public async Task ConnectAsync(Guid chatId)
    {
        _currentChatId = chatId;

        if (_hub is null || _hub.State == HubConnectionState.Disconnected)
        {
            if (_hub is not null)
                await _hub.DisposeAsync();

            var token = await _localStorage.GetItemAsStringAsync("authToken");
            token = token?.Trim('"');

            var apiBase = _http.BaseAddress!.ToString().TrimEnd('/');

            _hub = new HubConnectionBuilder()
                .WithUrl($"{apiBase}/hubs/chat", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(token);
                })
                .WithAutomaticReconnect()
                .Build();

            _hub.On<NewMessageEvent>("NewMessage", evt => OnNewMessage?.Invoke(NormalizeEvent(evt)));
            _hub.On<ChatReadEvent>("ChatRead", evt => OnChatRead?.Invoke(NormalizeReadEvent(evt)));
            _hub.On<ChatTypingEvent>("UserTyping", evt => OnTyping?.Invoke(evt));
            _hub.On<MessageDeletedEvent>("MessageDeleted", evt => OnMessageDeleted?.Invoke(evt));
            _hub.Reconnected += async _ =>
            {
                try
                {
                    if (_currentChatId.HasValue)
                        await _hub.InvokeAsync("JoinChat", _currentChatId.Value.ToString());
                }
                catch { }
            };

            await _hub.StartAsync();
        }

        await _hub.InvokeAsync("JoinChat", chatId.ToString());
    }

    public async Task LeaveChatAsync(Guid chatId)
    {
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("LeaveChat", chatId.ToString());
    }

    public async Task SendTypingAsync(Guid chatId, bool isTyping)
    {
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("SendTyping", chatId.ToString(), isTyping);
    }

    public async Task<List<ChatSummaryDto>> GetChatsAsync()
        => (await _http.GetFromJsonAsync<List<ChatSummaryDto>>("api/chats") ?? [])
            .Select(NormalizeChat)
            .ToList();

    public async Task<List<ChatMessageDto>> GetMessagesAsync(Guid chatId, int skip = 0, int take = 50)
        => (await _http.GetFromJsonAsync<List<ChatMessageDto>>($"api/chats/{chatId}/messages?skip={skip}&take={take}") ?? [])
            .Select(NormalizeMessage)
            .ToList();

    public async Task<Guid> CreateDirectAsync(string targetUserId)
    {
        var r = await _http.PostAsJsonAsync("api/chats/direct", new CreateDirectChatRequest(targetUserId));
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<Guid>();
    }

    public async Task<Guid> CreateGroupAsync(string name, string[] memberIds)
    {
        var r = await _http.PostAsJsonAsync("api/chats/group", new CreateGroupChatRequest(name, memberIds));
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<Guid>();
    }

    public async Task<ChatMessageDto?> SendMessageAsync(Guid chatId, string text)
    {
        var r = await _http.PostAsJsonAsync($"api/chats/{chatId}/messages", new SendMessageRequest(text));
        r.EnsureSuccessStatusCode();
        var message = await r.Content.ReadFromJsonAsync<ChatMessageDto>();
        return message is null ? null : NormalizeMessage(message);
    }

    public async Task<ChatMessageDto?> SendFilesAsync(Guid chatId, IReadOnlyList<IBrowserFile> files, string? text = null)
    {
        foreach (var file in files)
        {
            var validationError = FileUploadRules.Validate(file, _l);
            if (validationError is not null)
                throw new InvalidOperationException(validationError);
        }

        using var content = new MultipartFormDataContent();
        if (!string.IsNullOrWhiteSpace(text))
            content.Add(new StringContent(text), "text");

        foreach (var file in files)
        {
            var streamContent = new StreamContent(file.OpenReadStream(FileUploadRules.GetMaxReadSize(file)));
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            content.Add(streamContent, "files", file.Name);
        }

        var r = await _http.PostAsync($"api/chats/{chatId}/attachments", content);
        r.EnsureSuccessStatusCode();
        var message = await r.Content.ReadFromJsonAsync<ChatMessageDto>();
        return message is null ? null : NormalizeMessage(message);
    }

    public async Task DeleteMessageAsync(Guid chatId, Guid msgId)
        => (await _http.DeleteAsync($"api/chats/{chatId}/messages/{msgId}")).EnsureSuccessStatusCode();

    public async Task MarkReadAsync(Guid chatId)
        => (await _http.PutAsync($"api/chats/{chatId}/read", null)).EnsureSuccessStatusCode();

    public async Task DeleteChatAsync(Guid chatId)
        => (await _http.DeleteAsync($"api/chats/{chatId}")).EnsureSuccessStatusCode();

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }

    private static ChatSummaryDto NormalizeChat(ChatSummaryDto chat)
        => chat with
        {
            LastMessageAt = chat.LastMessageAt.HasValue
                ? KyrgyzstanTime.ConvertFromApi(chat.LastMessageAt.Value)
                : null
        };

    private static ChatMessageDto NormalizeMessage(ChatMessageDto message)
        => message with
        {
            SentAt = KyrgyzstanTime.ConvertFromApi(message.SentAt),
            Attachments = message.Attachments?.Select(NormalizeAttachment).ToList()
        };

    private static NewMessageEvent NormalizeEvent(NewMessageEvent evt)
        => evt with { Message = NormalizeMessage(evt.Message) };

    private static ChatReadEvent NormalizeReadEvent(ChatReadEvent evt)
        => evt with { ReadAt = KyrgyzstanTime.ConvertFromApi(evt.ReadAt) };

    private static FileAttachmentDto NormalizeAttachment(FileAttachmentDto attachment)
        => attachment with
        {
            UploadedAt = KyrgyzstanTime.ConvertFromApi(attachment.UploadedAt)
        };
}
