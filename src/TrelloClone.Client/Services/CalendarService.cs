using System.Net.Http.Json;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class CalendarService(HttpClient http)
{
    public async Task<List<CalendarEventDto>> GetEventsAsync(DateTime? from = null, DateTime? to = null)
    {
        var qs = new List<string>();
        if (from.HasValue)
        {
            var fromUtc = KyrgyzstanTime.ConvertToUtc(from.Value);
            qs.Add($"from={Uri.EscapeDataString(fromUtc.ToString("O"))}");
        }
        if (to.HasValue)
        {
            var toUtc = KyrgyzstanTime.ConvertToUtc(to.Value);
            qs.Add($"to={Uri.EscapeDataString(toUtc.ToString("O"))}");
        }
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
        var normalizedReq = req with
        {
            StartTime = KyrgyzstanTime.ConvertToUtc(req.StartTime),
            EndTime = KyrgyzstanTime.ConvertToUtc(req.EndTime)
        };
        var r = await http.PostAsJsonAsync("api/calendar", normalizedReq);
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(await r.Content.ReadAsStringAsync());

        var ev = await r.Content.ReadFromJsonAsync<CalendarEventDto>();
        return ev is null ? null : NormalizeEvent(ev);
    }

    public async Task<CalendarEventDto?> UpdateAsync(Guid id, UpdateCalendarEventRequest req)
    {
        var normalizedReq = req with
        {
            StartTime = KyrgyzstanTime.ConvertToUtc(req.StartTime),
            EndTime = KyrgyzstanTime.ConvertToUtc(req.EndTime)
        };
        var r = await http.PutAsJsonAsync($"api/calendar/{id}", normalizedReq);
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(await r.Content.ReadAsStringAsync());

        var ev = await r.Content.ReadFromJsonAsync<CalendarEventDto>();
        return ev is null ? null : NormalizeEvent(ev);
    }

    public async Task DeleteAsync(Guid id)
        => (await http.DeleteAsync($"api/calendar/{id}")).EnsureSuccessStatusCode();

    private static CalendarEventDto NormalizeEvent(CalendarEventDto ev)
        => ev with
        {
            StartTime = KyrgyzstanTime.ConvertFromApi(ev.StartTime),
            EndTime = KyrgyzstanTime.ConvertFromApi(ev.EndTime)
        };
}
