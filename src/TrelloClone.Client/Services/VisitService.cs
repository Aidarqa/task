using System.Net.Http.Json;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class VisitService(HttpClient http)
{
    public async Task<List<VisitDto>> GetAllAsync()
        => (await http.GetFromJsonAsync<List<VisitDto>>("api/visits") ?? [])
            .Select(Normalize).ToList();

    public async Task<VisitDto?> GetAsync(Guid id)
    {
        var v = await http.GetFromJsonAsync<VisitDto>($"api/visits/{id}");
        return v is null ? null : Normalize(v);
    }

    public async Task<VisitDto?> CreateAsync(CreateVisitRequest req)
    {
        var normalized = req with
        {
            PlannedArrival   = KyrgyzstanTime.ConvertToUtc(req.PlannedArrival),
            PlannedDeparture = KyrgyzstanTime.ConvertToUtc(req.PlannedDeparture)
        };
        var r = await http.PostAsJsonAsync("api/visits", normalized);
        r.EnsureSuccessStatusCode();
        var v = await r.Content.ReadFromJsonAsync<VisitDto>();
        return v is null ? null : Normalize(v);
    }

    public async Task<VisitDto?> ApproveAsync(Guid id, string? comment = null)
    {
        var r = await http.PutAsJsonAsync($"api/visits/{id}/approve", new ApproveVisitRequest(comment));
        await EnsureSuccessAsync(r);
        var v = await r.Content.ReadFromJsonAsync<VisitDto>();
        return v is null ? null : Normalize(v);
    }

    public async Task<VisitDto?> RejectAsync(Guid id, string comment)
    {
        var r = await http.PutAsJsonAsync($"api/visits/{id}/reject", new RejectVisitRequest(comment));
        await EnsureSuccessAsync(r);
        var v = await r.Content.ReadFromJsonAsync<VisitDto>();
        return v is null ? null : Normalize(v);
    }

    public async Task<VisitDto?> UpdateAsync(Guid id, CreateVisitRequest req)
    {
        var normalized = req with
        {
            PlannedArrival   = KyrgyzstanTime.ConvertToUtc(req.PlannedArrival),
            PlannedDeparture = KyrgyzstanTime.ConvertToUtc(req.PlannedDeparture)
        };
        var r = await http.PutAsJsonAsync($"api/visits/{id}", normalized);
        await EnsureSuccessAsync(r);
        var v = await r.Content.ReadFromJsonAsync<VisitDto>();
        return v is null ? null : Normalize(v);
    }

    public async Task DeleteAsync(Guid id)
    {
        var r = await http.DeleteAsync($"api/visits/{id}");
        await EnsureSuccessAsync(r);
    }

    /// Reads the response body on error and throws with the server's message.
    private static async Task EnsureSuccessAsync(HttpResponseMessage r)
    {
        if (r.IsSuccessStatusCode) return;
        var body = await r.Content.ReadAsStringAsync();
        // Strip JSON quotes if the server returned a plain string
        var message = body.Trim().TrimStart('"').TrimEnd('"');
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
            ? $"Ошибка сервера: {(int)r.StatusCode}"
            : message);
    }

    public async Task<bool> CheckRoomAvailableAsync(Guid resourceId, DateTime from, DateTime to)
    {
        var f = KyrgyzstanTime.ConvertToUtc(from).ToString("o");
        var t = KyrgyzstanTime.ConvertToUtc(to).ToString("o");
        var result = await http.GetFromJsonAsync<RoomAvailabilityResult>(
            $"api/visits/check-room?resourceId={resourceId}&from={Uri.EscapeDataString(f)}&to={Uri.EscapeDataString(t)}");
        return result?.Available ?? true;
    }

    private record RoomAvailabilityResult(bool Available);

    private static VisitDto Normalize(VisitDto v) => v with
    {
        PlannedArrival   = KyrgyzstanTime.ConvertFromApi(v.PlannedArrival),
        PlannedDeparture = KyrgyzstanTime.ConvertFromApi(v.PlannedDeparture),
        ApprovedAt       = v.ApprovedAt.HasValue ? KyrgyzstanTime.ConvertFromApi(v.ApprovedAt.Value) : null,
        CreatedAt        = KyrgyzstanTime.ConvertFromApi(v.CreatedAt)
    };
}
