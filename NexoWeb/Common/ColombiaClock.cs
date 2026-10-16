namespace NexoWeb.Common;

public static class ColombiaClock
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SA Pacific Standard Time" : "America/Bogota");

    public static DateTime Now   => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz);
    public static DateTime Today => Now.Date;
}
