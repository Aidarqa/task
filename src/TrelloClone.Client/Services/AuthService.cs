using System.Net.Http.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<HttpResponseMessage> ChangePasswordAsync(ChangePasswordRequest request);
    Task<string?> GetUserIdAsync();
    Task<string?> GetUserNameAsync();
}

public class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;
    private readonly AuthenticationStateProvider _authState;

    public AuthService(HttpClient http, ILocalStorageService localStorage,
        AuthenticationStateProvider authState)
    {
        _http = http;
        _localStorage = localStorage;
        _authState = authState;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", request);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

        if (result is { Success: true, Token: not null })
        {
            await _localStorage.SetItemAsStringAsync("authToken", result.Token);
            await _localStorage.SetItemAsStringAsync("userId", result.UserId ?? "");
            await _localStorage.SetItemAsStringAsync("userName", result.UserName ?? "");
            ((JwtAuthStateProvider)_authState).NotifyAuthStateChanged();
        }

        return result ?? new AuthResponse(false, null, null, null, "Unknown error");
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync("authToken");
        await _localStorage.RemoveItemAsync("userId");
        await _localStorage.RemoveItemAsync("userName");
        ((JwtAuthStateProvider)_authState).NotifyAuthStateChanged();
    }

    public Task<HttpResponseMessage> ChangePasswordAsync(ChangePasswordRequest request) =>
        _http.PostAsJsonAsync("api/auth/change-password", request);

    public async Task<string?> GetUserIdAsync() =>
        await _localStorage.GetItemAsStringAsync("userId");

    public async Task<string?> GetUserNameAsync() =>
        await _localStorage.GetItemAsStringAsync("userName");
}
