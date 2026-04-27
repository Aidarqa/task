namespace TrelloClone.Client.Services;

public static class KyrgyzstanTime
{
    private static readonly TimeSpan UtcOffset = TimeSpan.FromHours(6);

    public const string TimeZoneId = "Asia/Bishkek";

    public static DateTime Now => ConvertFromApi(DateTime.UtcNow);

    public static DateTime Today => Now.Date;

    public static string FormatDateTime(DateTime value)
        => ForDisplay(value).ToString("dd.MM.yyyy HH:mm");

    public static string FormatDate(DateTime value)
        => ForDisplay(value).ToString("dd.MM.yyyy");

    public static string FormatDateSmart(DateTime value)
    {
        var displayValue = ForDisplay(value);
        return displayValue.TimeOfDay == TimeSpan.Zero
            ? displayValue.ToString("dd.MM.yyyy")
            : displayValue.ToString("dd.MM.yyyy HH:mm");
    }

    public static string FormatDateTimeRange(DateTime start, DateTime end)
        => $"{FormatDateTime(start)} - {FormatDateTime(end)}";

    public static DateTime ConvertFromApi(DateTime value)
    {
        // API values are UTC by convention. JSON/SQLite can drop or change Kind,
        // so keep the clock value and apply the fixed Bishkek UTC+6 offset once.
        var utcValue = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return DateTime.SpecifyKind(utcValue + UtcOffset, DateTimeKind.Unspecified);
    }

    public static DateTime? ConvertFromApi(DateTime? value)
        => value.HasValue ? ConvertFromApi(value.Value) : null;

    public static DateTime ConvertToUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return value;

        // Treat UI-entered wall-clock values as Bishkek local time,
        // regardless of the browser or host machine timezone.
        var utcValue = DateTime.SpecifyKind(value, DateTimeKind.Unspecified) - UtcOffset;
        return DateTime.SpecifyKind(utcValue, DateTimeKind.Utc);
    }

    public static DateTime? ConvertToUtc(DateTime? value)
        => value.HasValue ? ConvertToUtc(value.Value) : null;

    private static DateTime ForDisplay(DateTime value)
        => value.Kind is DateTimeKind.Utc or DateTimeKind.Local
            ? ConvertFromApi(value)
            : value;
}
