using System.Net.Http.Json;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public class BookingService(HttpClient http, LocalizationService l)
{
    public async Task<List<Resource>> GetResourcesAsync()
        => await http.GetFromJsonAsync<List<Resource>>("api/bookings/resources") ?? [];

    public async Task<Resource?> CreateResourceAsync(CreateResourceRequest req)
    {
        var r = await http.PostAsJsonAsync("api/bookings/resources", req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<Resource>();
    }

    public async Task<Resource?> UpdateResourceAsync(Guid id, UpdateResourceRequest req)
    {
        var r = await http.PutAsJsonAsync($"api/bookings/resources/{id}", req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<Resource>();
    }

    public async Task DeleteResourceAsync(Guid id)
        => (await http.DeleteAsync($"api/bookings/resources/{id}")).EnsureSuccessStatusCode();

    public async Task<List<BookingDto>> GetBookingsAsync(DateTime? from = null, DateTime? to = null, Guid? resourceId = null)
    {
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"from={from.Value:O}");
        if (to.HasValue) qs.Add($"to={to.Value:O}");
        if (resourceId.HasValue) qs.Add($"resourceId={resourceId}");
        var url = "api/bookings" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        var bookings = await http.GetFromJsonAsync<List<BookingDto>>(url) ?? [];
        return bookings.Select(NormalizeBooking).ToList();
    }

    public async Task<BookingDto?> CreateBookingAsync(CreateBookingRequest req)
    {
        var r = await http.PostAsJsonAsync("api/bookings", req);
        if (r.StatusCode == System.Net.HttpStatusCode.Conflict)
            throw new InvalidOperationException(l["book_conflict"]);
        r.EnsureSuccessStatusCode();
        var booking = await r.Content.ReadFromJsonAsync<BookingDto>();
        return booking is null ? null : NormalizeBooking(booking);
    }

    public async Task CancelBookingAsync(Guid id)
        => (await http.PutAsync($"api/bookings/{id}/cancel", null)).EnsureSuccessStatusCode();

    public async Task DeleteBookingAsync(Guid id)
        => (await http.DeleteAsync($"api/bookings/{id}")).EnsureSuccessStatusCode();

    private static BookingDto NormalizeBooking(BookingDto booking)
        => booking with
        {
            StartTime = NormalizeScheduleTime(booking.StartTime),
            EndTime = NormalizeScheduleTime(booking.EndTime),
            CreatedAt = KyrgyzstanTime.ConvertFromApi(booking.CreatedAt)
        };

    private static DateTime NormalizeScheduleTime(DateTime value)
        => value.Kind == DateTimeKind.Utc ? KyrgyzstanTime.ConvertFromApi(value) : value;
}
