using System.Net.Http.Json;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class ProjectService(HttpClient http)
{
    public async Task<List<Project>> GetAllAsync()
        => (await http.GetFromJsonAsync<List<Project>>("api/projects") ?? [])
            .Select(NormalizeProject)
            .ToList();

    public async Task<Project?> GetAsync(Guid id)
    {
        var project = await http.GetFromJsonAsync<Project>($"api/projects/{id}");
        return project is null ? null : NormalizeProject(project);
    }

    public async Task<Project?> CreateAsync(CreateProjectRequest req)
    {
        var normalizedRequest = req with
        {
            StartDate = KyrgyzstanTime.ConvertToUtc(req.StartDate),
            EndDate = KyrgyzstanTime.ConvertToUtc(req.EndDate)
        };

        var r = await http.PostAsJsonAsync("api/projects", normalizedRequest);
        r.EnsureSuccessStatusCode();
        var project = await r.Content.ReadFromJsonAsync<Project>();
        return project is null ? null : NormalizeProject(project);
    }

    public async Task<Project?> UpdateAsync(Guid id, UpdateProjectRequest req)
    {
        var normalizedRequest = req with
        {
            StartDate = KyrgyzstanTime.ConvertToUtc(req.StartDate),
            EndDate = KyrgyzstanTime.ConvertToUtc(req.EndDate)
        };

        var r = await http.PutAsJsonAsync($"api/projects/{id}", normalizedRequest);
        r.EnsureSuccessStatusCode();
        var project = await r.Content.ReadFromJsonAsync<Project>();
        return project is null ? null : NormalizeProject(project);
    }

    public async Task DeleteAsync(Guid id)
        => (await http.DeleteAsync($"api/projects/{id}")).EnsureSuccessStatusCode();

    public async Task<List<WorkTaskSummary>> GetTasksAsync(Guid id)
        => (await http.GetFromJsonAsync<List<WorkTaskSummary>>($"api/projects/{id}/tasks") ?? [])
            .Select(NormalizeTask)
            .ToList();

    private static Project NormalizeProject(Project project)
    {
        project.StartDate = KyrgyzstanTime.ConvertFromApi(project.StartDate);
        project.EndDate = KyrgyzstanTime.ConvertFromApi(project.EndDate);
        project.CreatedAt = KyrgyzstanTime.ConvertFromApi(project.CreatedAt);
        return project;
    }

    private static WorkTaskSummary NormalizeTask(WorkTaskSummary task)
        => task with
        {
            DueDate = KyrgyzstanTime.ConvertFromApi(task.DueDate),
            CreatedAt = KyrgyzstanTime.ConvertFromApi(task.CreatedAt)
        };
}
