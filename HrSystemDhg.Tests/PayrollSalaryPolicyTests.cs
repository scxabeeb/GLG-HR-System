using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Xunit;

public class PayrollSalaryPolicyTests
{
    [Theory]
    [InlineData(60, 1.92)]
    [InlineData(480, 15.38)]
    [InlineData(0, 0)]
    public void MissingMinutesUseTheMonthlyBasicSalary(int missing, double expected)
    {
        var rule = new WorkRule { DailyHours = 8, MinuteRateMode = "Fixed", FixedMinuteRate = 99, SalaryDivisor = 30 };
        Assert.Equal((decimal)expected, decimal.Round(PayrollSalaryPolicy.Deduction(rule, 400m, missing, 26), 2));
    }
}
