using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Xunit;

namespace HrSystemDhg.Tests;
public class PayrollPolicyTests
{
    [Theory]
    [InlineData(true, true, true, 120, 0, 0)]
    [InlineData(true, false, true, 120, 0, 100)]
    [InlineData(false, false, true, 0, 100, 0)]
    [InlineData(false, false, false, 30, 15, 0)]
    [InlineData(false, false, false, -10, 0, 0)]
    public void LeaveAndAttendanceAreNotDoubleCounted(bool approved, bool paid, bool absent, int late, decimal attendance, decimal unpaid)
    {
        var result = PayrollPolicy.DayDeductions(100, approved, paid, absent, late, 1, .5m);
        Assert.Equal(attendance, result.Attendance);
        Assert.Equal(unpaid, result.Unpaid);
    }

    [Theory]
    [InlineData(true, 850)]
    [InlineData(false, 950)]
    public void AttendanceWaiverDoesNotWaiveUnpaidLeave(bool apply, decimal net) => Assert.Equal(net, PayMath.Calculate(1000, 100, 50, apply).net);
    [Fact]
    public void NetPayCannotBeNegative() => Assert.Equal(0, PayMath.Calculate(100, 100, 50, true).net);
    [Fact]
    public void NightShiftBelongsToItsStartingDate()
    {
        var day = new DateTime(2026, 10, 19);
        var result = CalendarService.Window(new() { StartHour = 22, DailyHours = 8, BreakMinutes = 0 }, day);
        Assert.Equal(day.AddDays(1).AddHours(6), result.End);
    }

    [Fact]
    public void PartialLeaveAcrossMidnightUsesFourHours()
    {
        var r = new LeaveRequest
        {
            From = new(2026, 10, 19),
            StartTime = TimeSpan.FromHours(22),
            EndTime = TimeSpan.FromHours(2)
        };
        Assert.Equal(.5m, CalendarService.LeaveFraction(r, new() { StartHour = 22, DailyHours = 8, BreakMinutes = 0 }));
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData(" +SUM(A1:A2)")]
    [InlineData("@SUM(A1)")]
    [InlineData("-1+2")]
    public void CsvDoesNotExecuteUntrustedFormulaText(string value) => Assert.StartsWith("'", ExportService.Safe(value));
    [Fact]
    public void PlainNamesAreUnmodified() => Assert.Equal("Amina Yusuf", ExportService.Safe("Amina Yusuf"));
}
