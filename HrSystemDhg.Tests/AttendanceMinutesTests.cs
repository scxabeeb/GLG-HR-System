using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Xunit;

public class AttendanceMinutesTests
{
    private static readonly DateTime Day = new(2026, 11, 23);
    private static readonly WorkRule Rule = new() { UsePunchWindows = true, CreditFullDayForValidPunches = false, CheckOutFrom = TimeSpan.FromHours(11.5), StartHour = 6, StartMinute = 30, DailyHours = 8, BreakMinutes = 0, WorkingWeekdays = "0,1,2,3,4,6" };
    [Theory]
    [InlineData(6, 30, true)] [InlineData(10, 30, true)] [InlineData(10, 31, false)] [InlineData(6, 29, false)]
    public void CheckInWindow(int hour, int minute, bool allowed) => Assert.Equal(allowed, AttendanceMinutes.CheckIn(Rule, Day.AddHours(hour).AddMinutes(minute)));
    [Theory]
    [InlineData(11, 29, false)] [InlineData(11, 30, true)] [InlineData(15, 30, true)] [InlineData(15, 31, false)]
    public void CheckOutWindow(int hour, int minute, bool allowed) => Assert.Equal(allowed, AttendanceMinutes.CheckOut(Rule, Day.AddHours(hour).AddMinutes(minute)));
    [Theory]
    [InlineData(6.5, 14.5, 0)] [InlineData(7.5, 15.5, 0)] [InlineData(10.5, 15.5, 180)] [InlineData(6.5, 15.5, 0)] [InlineData(6.5, 11.5, 180)]
    public void ActualWorkedMinutesDetermineShortfall(double start, double end, int missing) => Assert.Equal(missing, AttendanceMinutes.Missing(Rule, Day, Day.AddHours(start), Day.AddHours(end), []));
    [Fact]
    public void PaidLeaveDoesNotBecomeMissingWork()
    {
        var leaves = new[] { new LeaveRequest { From = Day, To = Day, StartTime = TimeSpan.FromHours(6.5), EndTime = TimeSpan.FromHours(10.5) } };
        Assert.Equal(0, AttendanceMinutes.Missing(Rule, Day, Day.AddHours(10.5), Day.AddHours(14.5), leaves));
        Assert.Equal(0, AttendanceMinutes.Missing(Rule, Day, Day.AddHours(10.5), Day.AddHours(14.5), leaves.Concat(leaves)));
    }
    [Fact]
    public void FullAbsenceAndPartialMinutesUseThirtyDayRate()
    {
        Assert.Equal(6.25m, AttendanceMinutes.Deduction(Rule, 1500m, 60));
        Assert.Equal(50m, AttendanceMinutes.Deduction(Rule, 1500m, 480));
        Assert.Equal(50m, AttendanceMinutes.Deduction(Rule, 1500m, 600));
    }
    [Fact]
    public void FridayIsTheOnlyWeekendDay()
    {
        for (var i = 0; i < 7; i++) Assert.Equal(Day.AddDays(i).DayOfWeek != DayOfWeek.Friday, CalendarService.Weekday(Rule, Day.AddDays(i)));
    }
    [Fact]
    public void SettingsControlTimeWindowsHoursDivisorAndFixedRate()
    {
        var custom = new WorkRule { UsePunchWindows = true, CheckInFrom = TimeSpan.FromHours(7), CheckInUntil = TimeSpan.FromHours(11), CheckOutFrom = TimeSpan.FromHours(12), CheckOutUntil = TimeSpan.FromHours(17), DailyHours = 7, BreakMinutes = 30, SalaryDivisor = 26 };
        Assert.True(AttendanceMinutes.CheckIn(custom, Day.AddHours(11)));
        Assert.False(AttendanceMinutes.CheckIn(custom, Day.AddHours(6.5)));
        Assert.True(AttendanceMinutes.CheckOut(custom, Day.AddHours(16)));
        Assert.Equal(7.14m, decimal.Round(AttendanceMinutes.Deduction(custom, 1300m, 60), 2));
        custom.MinuteRateMode = "Fixed"; custom.FixedMinuteRate = .125m;
        Assert.Equal(7.5m, AttendanceMinutes.Deduction(custom, 1300m, 60));
        Assert.Equal(52.5m, AttendanceMinutes.Deduction(custom, 1300m, 500));
        Assert.True(AttendanceMinutes.Valid(custom));
        custom.CheckOutFrom = TimeSpan.FromHours(10);
        Assert.False(AttendanceMinutes.Valid(custom));
        custom.SalaryDivisor = 0;
        Assert.False(AttendanceMinutes.Valid(custom));
    }
    [Fact]
    public void UnpaidLeaveUsesSalaryValueEvenWhenAttendanceRateIsZero()
    {
        var rule = new WorkRule { StartHour = 6, StartMinute = 30, DailyHours = 8, BreakMinutes = 0, SalaryDivisor = 30, MinuteRateMode = "Fixed", FixedMinuteRate = 0 };
        var full = new LeaveRequest { From = Day, To = Day };
        var half = new LeaveRequest { From = Day, To = Day, StartTime = TimeSpan.FromHours(6.5), EndTime = TimeSpan.FromHours(10.5) };
        Assert.Equal(50m, AttendanceMinutes.UnpaidLeaveDeduction(rule, 1500m, Day, [full]));
        Assert.Equal(25m, AttendanceMinutes.UnpaidLeaveDeduction(rule, 1500m, Day, [half]));
        Assert.Equal(25m, AttendanceMinutes.UnpaidLeaveDeduction(rule, 1500m, Day, [half, half]));
        Assert.Equal(0m, AttendanceMinutes.UnpaidLeaveDeduction(rule, 1500m, Day, []));
    }
    [Theory]
    [InlineData(6.5, 12.5)]
    [InlineData(10.5, 12.5)]
    [InlineData(10.5, 15.5)]
    public void AnyValidWindowPairCreditsEightHours(double first, double last)
    {
        var rule = new WorkRule { UsePunchWindows = true, DailyHours = 8, BreakMinutes = 0 };
        Assert.Equal(0, AttendanceMinutes.Missing(rule,Day,Day.AddHours(first),Day.AddHours(last),[]));
        Assert.Equal(8m, AttendanceMinutes.CreditedHours(rule,Day,Day.AddHours(first),Day.AddHours(last),[]));
        Assert.Equal(0m, PayrollSalaryPolicy.Deduction(rule,400m,AttendanceMinutes.Missing(rule,Day,Day.AddHours(first),Day.AddHours(last),[]),26));
    }
    [Fact]
    public void InvalidOrMissingWindowPunchDoesNotEarnFullCredit()
    {
        var rule = new WorkRule { UsePunchWindows = true, DailyHours = 8 };
        Assert.False(AttendanceMinutes.CheckOut(rule,Day.AddHours(12).AddMinutes(29)));
        Assert.True(AttendanceMinutes.CheckOut(rule,Day.AddHours(12.5)));
        Assert.False(AttendanceMinutes.ValidPair(rule,Day.AddHours(10.5),Day.AddHours(11.5)));
        Assert.Equal(480,AttendanceMinutes.Missing(rule,Day,Day.AddHours(10.5),null,[]));
        Assert.Equal(0m,AttendanceMinutes.CreditedHours(rule,Day,null,Day.AddHours(12.5),[]));
    }

    [Fact]
    public void MissingCheckoutWaitsForInclusiveDeadlineThenBecomesAbsent()
    {
        var rule = new WorkRule { UsePunchWindows = true, DailyHours = 8 };
        var day = new AttendanceDay { Date = Day, FirstIn = Day.AddHours(8), Status = "Incomplete punch", MissingMinutes = 480 };
        AttendanceMinutes.ApplyClosingPhase(rule, day, Day.AddHours(15.5).AddSeconds(59));
        Assert.Equal("Pending check-out", day.Status);
        Assert.Equal(0, day.MissingMinutes);
        // Rebuild calculates missing minutes again before applying the closing phase.
        day.MissingMinutes = AttendanceMinutes.Missing(rule, Day, day.FirstIn, null, []);
        AttendanceMinutes.ApplyClosingPhase(rule, day, Day.AddHours(15).AddMinutes(31));
        Assert.Equal("Absent", day.Status);
        Assert.Equal(480, day.MissingMinutes);
        Assert.Contains("check-out missing", day.StatusMessage);
        Assert.Null(day.LastOut);
    }
    [Fact]
    public void FutureAttendanceAndApprovedLeaveAreProtected()
    {
        var rule = new WorkRule { UsePunchWindows = true };
        var day = new AttendanceDay { Date = Day, Status = "Absent", MissingMinutes = 480 };
        AttendanceMinutes.ApplyClosingPhase(rule, day, Day.AddDays(-1));
        Assert.Equal("Not due", day.Status);
        Assert.Equal(0, day.MissingMinutes);
        day.Status = "On leave";
        AttendanceMinutes.ApplyClosingPhase(rule, day, Day.AddDays(1));
        Assert.Equal("On leave", day.Status);
    }
}
