using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Xunit;

public class AttendanceMinutesTests
{
    private static readonly DateTime Day = new(2026, 11, 23);
    private static readonly WorkRule Rule = new() { UsePunchWindows = true, StartHour = 6, StartMinute = 30, DailyHours = 8, BreakMinutes = 0, WorkingWeekdays = "0,1,2,3,4,6" };
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
}
