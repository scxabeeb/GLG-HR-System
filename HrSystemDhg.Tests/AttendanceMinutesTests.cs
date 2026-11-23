using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Xunit;

public class AttendanceMinutesTests
{
    private static readonly DateTime Day = new(2026, 11, 23);
    private static readonly WorkRule Rule = new() { UsePunchWindows = true, StartHour = 6, StartMinute = 30, DailyHours = 8, BreakMinutes = 0, WorkingWeekdays = "0,1,2,3,4,6" };
    [Theory]
    [InlineData(6, 30, true)] [InlineData(10, 30, true)] [InlineData(10, 31, false)] [InlineData(6, 29, false)]
    public void CheckInWindow(int hour, int minute, bool allowed) => Assert.Equal(allowed, AttendanceMinutes.CheckIn(Day.AddHours(hour).AddMinutes(minute)));
    [Theory]
    [InlineData(11, 29, false)] [InlineData(11, 30, true)] [InlineData(15, 30, true)] [InlineData(15, 31, false)]
    public void CheckOutWindow(int hour, int minute, bool allowed) => Assert.Equal(allowed, AttendanceMinutes.CheckOut(Day.AddHours(hour).AddMinutes(minute)));
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
        Assert.Equal(6.25m, AttendanceMinutes.Deduction(1500m, 60));
        Assert.Equal(50m, AttendanceMinutes.Deduction(1500m, 480));
        Assert.Equal(50m, AttendanceMinutes.Deduction(1500m, 600));
    }
    [Fact]
    public void FridayIsTheOnlyWeekendDay()
    {
        for (var i = 0; i < 7; i++) Assert.Equal(Day.AddDays(i).DayOfWeek != DayOfWeek.Friday, CalendarService.Weekday(Rule, Day.AddDays(i)));
    }
}
