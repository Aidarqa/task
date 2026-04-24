namespace TrelloClone.Api.Services;

public static class KyrgyzstanTime
{
    private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

    public static DateTime Now => ConvertFromUtc(DateTime.UtcNow);

    public static DateTime Today => Now.Date;

    public static DateTime ConvertFromUtc(DateTime value)
    {
        var utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utcValue, TimeZone);
    }

    public static string FormatDateTime(DateTime value, string format)
        => ConvertFromUtc(value).ToString(format);

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
