using HrSystemDhg.Web.Models;
namespace HrSystemDhg.Web.Services;

public static class PayrollSalaryPolicy
{
    public const int DefaultSalaryDays = 26;
    public static decimal Rate(WorkRule rule, decimal basic, int days) => basic / days / (rule.DailyHours * 60m);
    public static decimal Deduction(WorkRule rule, decimal basic, int missing, int days) =>
        basic * Math.Clamp(missing, 0, AttendanceMinutes.Required(rule)) / days / (rule.DailyHours * 60m);
    public static string Formula(WorkRule rule, int days) => $"basic ÷ {days} ÷ {rule.DailyHours * 60m:0} minutes ({days * rule.DailyHours * 60m:0} monthly minutes)";
    public static decimal UnpaidLeave(WorkRule rule, decimal basic, DateTime date, IEnumerable<LeaveRequest> leave, int days) =>
        Deduction(rule, basic, AttendanceMinutes.Required(rule) - AttendanceMinutes.Missing(rule, date, null, null, leave), days);
}
