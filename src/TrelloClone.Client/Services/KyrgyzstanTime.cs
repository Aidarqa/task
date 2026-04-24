namespace TrelloClone.Client.Services;

public static class KyrgyzstanTime
{
    private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

    public static DateTime Now => ConvertFromApi(DateTime.UtcNow);

    public static DateTime Today => Now.Date;

    public static string FormatDateTime(DateTime value)
        => value.ToString("dd.MM.yyyy HH:mm");

    public static string FormatDateTimeRange(DateTime start, DateTime end)
        => $"{FormatDateTime(start)} - {FormatDateTime(end)}";

    public static DateTime ConvertFromApi(DateTime value)
    {
        // SQLite drops DateTimeKind, so the deserializer may produce Kind=Local or Unspecified
        // even though the stored value is always UTC. Force-treat every incoming value as UTC.
        var utcValue = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utcValue, TimeZone);
    }

    public static DateTime? ConvertFromApi(DateTime? value)
        => value.HasValue ? ConvertFromApi(value.Value) : null;

    public static DateTime ConvertToUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return value;

        // Treat UI-entered wall-clock values as Kyrgyzstan local time,
        // regardless of the browser or host machine timezone.
        var kyrgyzLocal = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(kyrgyzLocal, TimeZone);
    }

    public static DateTime? ConvertToUtc(DateTime? value)
        => value.HasValue ? ConvertToUtc(value.Value) : null;

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var timeZoneId in new[] { "Asia/Bishkek", "Central Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Asia/Bishkek",
            TimeSpan.FromHours(6),
            "Kyrgyzstan Time",
            "Kyrgyzstan Time");
    }
}
