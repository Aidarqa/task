using System.Net.Http.Json;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class AdminUserService
{
    private readonly HttpClient _http;
    public AdminUserService(HttpClient http) => _http = http;

    public async Task<List<AdminUserDto>> ListAsync() =>
        await _http.GetFromJsonAsync<List<AdminUserDto>>("api/admin/users") ?? [];

    public async Task<AdminUserDto?> GetAsync(string id) =>
        await _http.GetFromJsonAsync<AdminUserDto>($"api/admin/users/{id}");

    public async Task<HttpResponseMessage> CreateAsync(AdminCreateUserRequest req) =>
        await _http.PostAsJsonAsync("api/admin/users", req);

    public async Task<HttpResponseMessage> UpdateAsync(string id, AdminUpdateUserRequest req) =>
        await _http.PutAsJsonAsync($"api/admin/users/{id}", req);

    public async Task<HttpResponseMessage> ResetPasswordAsync(string id, AdminResetPasswordRequest req) =>
        await _http.PostAsJsonAsync($"api/admin/users/{id}/password", req);

    public async Task<HttpResponseMessage> DeleteAsync(string id) =>
        await _http.DeleteAsync($"api/admin/users/{id}");
}

public class RoleService
{
    private readonly HttpClient _http;
    public RoleService(HttpClient http) => _http = http;

    public async Task<List<RoleDto>> ListAsync() =>
        await _http.GetFromJsonAsync<List<RoleDto>>("api/admin/roles") ?? [];

    public async Task<RoleDto?> GetAsync(Guid id) =>
        await _http.GetFromJsonAsync<RoleDto>($"api/admin/roles/{id}");

    public async Task<List<PermissionDescriptor>> AllPermissionsAsync() =>
        await _http.GetFromJsonAsync<List<PermissionDescriptor>>("api/admin/roles/permissions") ?? [];

    public async Task<HttpResponseMessage> CreateAsync(CreateRoleRequest req) =>
        await _http.PostAsJsonAsync("api/admin/roles", req);

    public async Task<HttpResponseMessage> UpdateAsync(Guid id, UpdateRoleRequest req) =>
        await _http.PutAsJsonAsync($"api/admin/roles/{id}", req);

    public async Task<HttpResponseMessage> DeleteAsync(Guid id) =>
        await _http.DeleteAsync($"api/admin/roles/{id}");
}
