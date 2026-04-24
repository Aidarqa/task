using System.Net.Http.Json;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class TodoService(HttpClient http)
{
    public async Task<List<TodoList>> GetListsAsync()
        => (await http.GetFromJsonAsync<List<TodoList>>("api/todo/lists") ?? [])
            .Select(NormalizeList)
            .ToList();

    public async Task<TodoList?> CreateListAsync(string title)
    {
        var r = await http.PostAsJsonAsync("api/todo/lists", new CreateTodoListRequest(title));
        r.EnsureSuccessStatusCode();
        var list = await r.Content.ReadFromJsonAsync<TodoList>();
        return list is null ? null : NormalizeList(list);
    }

    public async Task UpdateListAsync(Guid id, string title)
        => (await http.PutAsJsonAsync($"api/todo/lists/{id}", new UpdateTodoListRequest(title))).EnsureSuccessStatusCode();

    public async Task DeleteListAsync(Guid id)
        => (await http.DeleteAsync($"api/todo/lists/{id}")).EnsureSuccessStatusCode();

    public async Task<TodoItem?> AddItemAsync(Guid listId, string text, DateTime? dueDate = null)
    {
        var r = await http.PostAsJsonAsync(
            $"api/todo/lists/{listId}/items",
            new CreateTodoItemRequest(text, KyrgyzstanTime.ConvertToUtc(dueDate)));
        r.EnsureSuccessStatusCode();
        var item = await r.Content.ReadFromJsonAsync<TodoItem>();
        return item is null ? null : NormalizeItem(item);
    }

    public async Task<TodoItem?> UpdateItemAsync(Guid id, string text, bool isCompleted, DateTime? dueDate)
    {
        var r = await http.PutAsJsonAsync(
            $"api/todo/items/{id}",
            new UpdateTodoItemRequest(text, isCompleted, KyrgyzstanTime.ConvertToUtc(dueDate)));
        r.EnsureSuccessStatusCode();
        var item = await r.Content.ReadFromJsonAsync<TodoItem>();
        return item is null ? null : NormalizeItem(item);
    }

    public async Task ToggleItemAsync(Guid id)
        => (await http.PutAsync($"api/todo/items/{id}/toggle", null)).EnsureSuccessStatusCode();

    public async Task DeleteItemAsync(Guid id)
        => (await http.DeleteAsync($"api/todo/items/{id}")).EnsureSuccessStatusCode();

    private static TodoList NormalizeList(TodoList list)
    {
        list.CreatedAt = KyrgyzstanTime.ConvertFromApi(list.CreatedAt);
        list.Items = list.Items.Select(NormalizeItem).ToList();
        return list;
    }

    private static TodoItem NormalizeItem(TodoItem item)
    {
        item.DueDate = KyrgyzstanTime.ConvertFromApi(item.DueDate);
        item.CreatedAt = KyrgyzstanTime.ConvertFromApi(item.CreatedAt);
        return item;
    }
}
