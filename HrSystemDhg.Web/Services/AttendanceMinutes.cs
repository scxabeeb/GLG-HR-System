using HrSystemDhg.Web.Models;

namespace HrSystemDhg.Web.Services;

public static class AttendanceMinutes
{
    // Device punches are stored as East Africa local wall-clock times, not UTC.
    public static bool CheckIn(WorkRule rule, DateTime time) => time.TimeOfDay >= rule.CheckInFrom && time.TimeOfDay < rule.CheckInUntil.Add(TimeSpan.FromMinutes(1));
    public static bool CheckOut(WorkRule rule, DateTime time) => time.TimeOfDay >= rule.CheckOutFrom && time.TimeOfDay < rule.CheckOutUntil.Add(TimeSpan.FromMinutes(1));
    public static bool CreditsFullDay(WorkRule rule) => rule.UsePunchWindows && rule.CreditFullDayForValidPunches;
    public static bool ValidPair(WorkRule rule, DateTime? first, DateTime? last) => first.HasValue && last.HasValue && first.Value.Date == last.Value.Date && CheckIn(rule, first.Value) && CheckOut(rule, last.Value) && last.Value > first.Value;
    public static decimal CreditedHours(WorkRule rule, DateTime date, DateTime? first, DateTime? last, IEnumerable<LeaveRequest> leaves) =>
        ValidPair(rule, first, last) ? Missing(rule, date, null, null, leaves) / 60m : 0;
    public static string PairMessage(WorkRule rule, DateTime? first, DateTime? last)
    {
        var validIn = first.HasValue && CheckIn(rule, first.Value);
        var validOut = last.HasValue && CheckOut(rule, last.Value);
        if (validIn && validOut && !ValidPair(rule, first, last)) return "Punches do not form a valid pair for the same work date.";
        return !validIn && !validOut ? "No valid check-in or check-out recorded." : !validIn ? "Check-out recorded; valid check-in missing." : !validOut ? "Check-in recorded; valid check-out missing." : "Check-in and check-out verified within allowed windows.";
    }
    public static int Required(WorkRule rule) => (int)(rule.DailyHours * 60m);
    public static decimal Rate(WorkRule rule, decimal monthlyBasic) => rule.MinuteRateMode == "Fixed" ? rule.FixedMinuteRate : monthlyBasic / rule.SalaryDivisor / (rule.DailyHours * 60m);
    public static string Formula(WorkRule rule) => rule.MinuteRateMode == "Fixed" ? $"fixed rate {rule.FixedMinuteRate:0.######} per minute" : $"basic ÷ {rule.SalaryDivisor:0.##} ÷ {rule.DailyHours * 60m:0} minutes";
    public static bool Valid(WorkRule rule) => rule.DailyHours > 0 && rule.DailyHours <= 20 && rule.SalaryDivisor >= 1 && rule.SalaryDivisor <= 366 && rule.FixedMinuteRate >= 0 &&
        (rule.MinuteRateMode == "Salary" || rule.MinuteRateMode == "Fixed") &&
        (!rule.UsePunchWindows || (rule.CheckInFrom >= TimeSpan.Zero && rule.CheckInFrom <= rule.CheckInUntil && rule.CheckInUntil < rule.CheckOutFrom && rule.CheckOutFrom <= rule.CheckOutUntil && rule.CheckOutUntil < TimeSpan.FromDays(1)));
    public static int Worked(DateTime? first, DateTime? last, int breakMinutes = 0) =>
        first.HasValue && last.HasValue ? Math.Max(0, (int)Math.Floor((last.Value - first.Value).TotalMinutes) - breakMinutes) : 0;
    public static int Missing(WorkRule rule, DateTime date, DateTime? first, DateTime? last, IEnumerable<LeaveRequest> leaves)
    {
        if (CreditsFullDay(rule))
        {
            if (ValidPair(rule, first, last)) return 0;
            first = last = null; // An invalid pair earns no attendance credit; approved leave still excuses its minutes.
        }
        var expected = (int)(rule.DailyHours * 60m);
        var ranges = leaves.Select(l => CalendarService.LeaveWindow(l, rule, date)).OrderBy(r => r.Start).ToList();
        // Merge overlapping leave windows so approval cannot be counted twice.
        var merged = new List<(DateTime Start, DateTime End)>();
        foreach (var range in ranges)
        {
            if (merged.Count == 0 || range.Start > merged[^1].End) merged.Add(range);
            else if (range.End > merged[^1].End) merged[^1] = (merged[^1].Start, range.End);
        }
        var excused = Math.Min(expected, merged.Sum(r => Math.Max(0, (int)(r.End - r.Start).TotalMinutes)));
        var overlap = first.HasValue && last.HasValue ? merged.Sum(r => Math.Max(0, (int)((last.Value < r.End ? last.Value : r.End) - (first.Value > r.Start ? first.Value : r.Start)).TotalMinutes)) : 0;
        return Math.Max(0, expected - excused - Math.Max(0, Worked(first, last, rule.BreakMinutes) - overlap));
    }
    public static decimal UnpaidLeaveDeduction(WorkRule rule, decimal monthlyBasic, DateTime date, IEnumerable<LeaveRequest> unpaidLeave)
    {
        var unpaidMinutes = Required(rule) - Missing(rule, date, null, null, unpaidLeave);
        return monthlyBasic * unpaidMinutes / (rule.SalaryDivisor * rule.DailyHours * 60m);
    }
    public static decimal Deduction(WorkRule rule, decimal monthlyBasic, int missingMinutes) => rule.MinuteRateMode == "Fixed"
        ? rule.FixedMinuteRate * Math.Clamp(missingMinutes, 0, Required(rule))
        : monthlyBasic * Math.Clamp(missingMinutes, 0, Required(rule)) / (rule.SalaryDivisor * rule.DailyHours * 60m);
}
