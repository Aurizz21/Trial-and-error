namespace PoultryOS.Services;

/// <summary>
/// "Today" for the business, always in Philippine time no matter where the server runs.
/// Sales are grouped by this date, so it must not drift with the host's time zone.
/// </summary>
public static class AppClock
{
    private static readonly TimeZoneInfo Zone = Resolve();

    public static DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone);

    public static DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    private static TimeZoneInfo Resolve()
    {
        // IANA id on Linux/macOS, Windows id as a fallback (Singapore is also UTC+8).
        foreach (var id in new[] { "Asia/Manila", "Singapore Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Local;
    }
}
