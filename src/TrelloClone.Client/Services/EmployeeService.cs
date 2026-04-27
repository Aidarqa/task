using System.Net.Http.Json;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class EmployeeService(HttpClient http)
{
    public async Task<List<UserDto>> GetAllAsync()
        => await http.GetFromJsonAsync<List<UserDto>>("api/employees") ?? [];

    public async Task<UserDto?> GetAsync(string id)
        => await http.GetFromJsonAsync<UserDto>($"api/employees/{id}");

    public async Task UpdateProfileAsync(string id, UpdateProfileRequest req)
        => (await http.PutAsJsonAsync($"api/employees/{id}/profile", req)).EnsureSuccessStatusCode();

    public async Task SetRoleAsync(string id, SystemRole role)
        => (await http.PutAsJsonAsync($"api/employees/{id}/role", new SetRoleRequest(role))).EnsureSuccessStatusCode();

    public async Task<List<Department>> GetDepartmentsAsync()
        => await http.GetFromJsonAsync<List<Department>>("api/employees/departments") ?? [];

    public async Task<Department?> CreateDepartmentAsync(CreateDepartmentRequest req)
    {
        var r = await http.PostAsJsonAsync("api/employees/departments", req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<Department>();
    }

    public async Task DeleteSelfAsync()
        => (await http.DeleteAsync("api/employees/me")).EnsureSuccessStatusCode();

    public async Task DeleteDepartmentAsync(Guid id)
        => (await http.DeleteAsync($"api/employees/departments/{id}")).EnsureSuccessStatusCode();
}
