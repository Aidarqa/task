using System.Net.Http.Json;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class DashboardService(HttpClient http)
{
    public async Task<DashboardStats?> GetStatsAsync()
    {
        var stats = await http.GetFromJsonAsync<DashboardStats>("api/dashboard");
        return stats is null ? null : NormalizeStats(stats);
    }

    private static DashboardStats NormalizeStats(DashboardStats stats)
        => stats with
        {
            RecentTasks = stats.RecentTasks.Select(NormalizeTask).ToList(),
            TodayEvents = stats.TodayEvents.Select(NormalizeEvent).ToList()
        };

    private static WorkTaskSummary NormalizeTask(WorkTaskSummary task)
        => task with
        {
            DueDate = KyrgyzstanTime.ConvertFromApi(task.DueDate),
            CreatedAt = KyrgyzstanTime.ConvertFromApi(task.CreatedAt)
        };

    private static CalendarEventDto NormalizeEvent(CalendarEventDto ev)
        => ev with
        {
            StartTime = KyrgyzstanTime.ConvertFromApi(ev.StartTime),
            EndTime = KyrgyzstanTime.ConvertFromApi(ev.EndTime)
        };
}
