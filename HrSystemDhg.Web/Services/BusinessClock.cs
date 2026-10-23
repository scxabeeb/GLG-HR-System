namespace HrSystemDhg.Web.Services;
public static class BusinessClock
{
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Africa/Mogadishu"));
    public static DateTime Today => Now.Date;
}
