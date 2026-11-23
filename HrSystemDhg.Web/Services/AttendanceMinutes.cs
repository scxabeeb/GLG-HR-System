using HrSystemDhg.Web.Models;

namespace HrSystemDhg.Web.Services;

public static class AttendanceMinutes
{
    // Device punches are stored as East Africa local wall-clock times, not UTC.
    public static bool CheckIn(DateTime time) => time.TimeOfDay >= new TimeSpan(6, 30, 0) && time.TimeOfDay < new TimeSpan(10, 31, 0);
    public static bool CheckOut(DateTime time) => time.TimeOfDay >= new TimeSpan(11, 30, 0) && time.TimeOfDay < new TimeSpan(15, 31, 0);
    public static int Worked(DateTime? first, DateTime? last, int breakMinutes = 0) =>
        first.HasValue && last.HasValue ? Math.Max(0, (int)Math.Floor((last.Value - first.Value).TotalMinutes) - breakMinutes) : 0;
    public static int Missing(WorkRule rule, DateTime date, DateTime? first, DateTime? last, IEnumerable<LeaveRequest> leaves)
    {
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
    public static decimal Deduction(decimal monthlyBasic, int missingMinutes) =>
        monthlyBasic * Math.Clamp(missingMinutes, 0, 480) / (30m * 480m);
}
