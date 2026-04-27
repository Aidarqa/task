namespace TrelloClone.Api.Services;

public static class KyrgyzstanTime
{
    private static readonly TimeSpan UtcOffset = TimeSpan.FromHours(6);

    public const string TimeZoneId = "Asia/Bishkek";

    public static DateTime Now => ConvertFromUtc(DateTime.UtcNow);

    public static DateTime Today => Now.Date;

    public static DateTime TodayUtcStart => ConvertToUtc(Today);

    public static DateTime TomorrowUtcStart => TodayUtcStart.AddDays(1);

    public static DateTime ConvertFromUtc(DateTime value)
    {
        return DateTime.SpecifyKind(NormalizeUtc(value) + UtcOffset, DateTimeKind.Unspecified);
    }

    public static DateTime ConvertToUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return value;

        var utcValue = DateTime.SpecifyKind(value, DateTimeKind.Unspecified) - UtcOffset;
        return DateTime.SpecifyKind(utcValue, DateTimeKind.Utc);
    }

    public static string FormatDateTime(DateTime value, string format)
        => ConvertFromUtc(value).ToString(format);

    public static DateTime NormalizeUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? NormalizeUtc(DateTime? value)
        => value.HasValue ? NormalizeUtc(value.Value) : null;

}
