using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Xunit;
public class AttendanceEligibilityTests
{
    [Fact]
    public void RegistrationUsesEastAfricaDateAndStartsFollowingDay()
    {
        var employee=new Employee { HireDate=new(2026,11,1), RegisteredUtc=new(2026,11,24,22,0,0,DateTimeKind.Utc) };
        Assert.Equal(new DateTime(2026,11,25),AttendanceEligibility.RegistrationDate(employee));
        Assert.Equal(new DateTime(2026,11,26),AttendanceEligibility.StartDate(employee));
    }
    [Fact]
    public void FutureHireDateStillControlsStart()
    {
        var employee=new Employee { HireDate=new(2026,12,1), RegisteredUtc=new(2026,11,24) };
        Assert.Equal(employee.HireDate,AttendanceEligibility.StartDate(employee));
    }
    [Fact]
    public void LegacyEmployeeFallsBackToDayAfterHireDate()
    {
        var employee=new Employee { HireDate=new(2026,11,24) };
        Assert.Equal(new DateTime(2026,11,25),AttendanceEligibility.StartDate(employee));
    }
}
