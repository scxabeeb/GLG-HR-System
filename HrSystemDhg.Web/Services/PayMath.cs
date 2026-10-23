namespace HrSystemDhg.Web.Services;
public static class PayrollPolicy
{
    public static bool IsWorkingDay(DateTime date) => date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    public static (decimal Attendance, decimal Unpaid) DayDeductions(decimal dailyRate, bool approvedLeave, bool paidLeave, bool absent, int lateMinutes, decimal absenceMultiplier, decimal latePerMinute)
    {
        if (approvedLeave)
            return (0, paidLeave ? 0 : dailyRate);
        return (absent ? dailyRate * absenceMultiplier : Math.Max(0, lateMinutes) * latePerMinute, 0);
    }
}
