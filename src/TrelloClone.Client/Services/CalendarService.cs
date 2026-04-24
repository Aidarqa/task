using System.Net.Http.Json;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class CalendarService(HttpClient http)
{
    public async Task<List<CalendarEventDto>> GetEventsAsync(DateTime? from = null, DateTime? to = null)
    {
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        if (to.HasValue) qs.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        var url = "api/calendar" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        var events = await http.GetFromJsonAsync<List<CalendarEventDto>>(url) ?? [];
        return events.Select(NormalizeEvent).ToList();
    }

    public async Task<CalendarEventDto?> GetAsync(Guid id)
    {
        var ev = await http.GetFromJsonAsync<CalendarEventDto>($"api/calendar/{id}");
        return ev is null ? null : NormalizeEvent(ev);
    }

    public async Task<CalendarEventDto?> CreateAsync(CreateCalendarEventRequest req)
    {
        var r = await http.PostAsJsonAsync("api/calendar", req);
        r.EnsureSuccessStatusCode();
        var ev = await r.Content.ReadFromJsonAsync<CalendarEventDto>();
        return ev is null ? null : NormalizeEvent(ev);
    }

    public async Task<CalendarEventDto?> UpdateAsync(Guid id, UpdateCalendarEventRequest req)
    {
        var r = await http.PutAsJsonAsync($"api/calendar/{id}", req);
        r.EnsureSuccessStatusCode();
        var ev = await r.Content.ReadFromJsonAsync<CalendarEventDto>();
        return ev is null ? null : NormalizeEvent(ev);
    }

    public async Task DeleteAsync(Guid id)
        => (await http.DeleteAsync($"api/calendar/{id}")).EnsureSuccessStatusCode();

    private static CalendarEventDto NormalizeEvent(CalendarEventDto ev)
        => ev with
        {
            StartTime = NormalizeScheduleTime(ev.StartTime),
            EndTime = NormalizeScheduleTime(ev.EndTime)
        };

    private static DateTime NormalizeScheduleTime(DateTime value)
        => value.Kind == DateTimeKind.Utc ? KyrgyzstanTime.ConvertFromApi(value) : value;
}
