using System.Security.Claims;
using HrSystemDhg.Web.Services;
using Xunit;

public class PayrollWorkflowTests
{
    private static ClaimsPrincipal Actor(string role) => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));

    [Theory]
    [InlineData("HR", "Calculated", "Submit", false)]
    [InlineData("Finance", "Calculated", "Submit", true)]
    [InlineData("Admin", "Calculated", "Submit", true)]
    [InlineData("Management", "Calculated", "Submit", false)]
    [InlineData("General Secretary", "Calculated", "Approve", false)]
    [InlineData("General Secretary", "Submitted", "Approve", true)]
    [InlineData("Admin", "Submitted", "Approve", false)]
    [InlineData("Finance", "Submitted", "Approve", false)]
    [InlineData("Finance", "Calculated", "Reject", true)]
    [InlineData("Finance", "Submitted", "Reject", false)]
    [InlineData("General Secretary", "Submitted", "Reject", true)]
    [InlineData("General Secretary", "Calculated", "Reject", false)]
    [InlineData("Admin", "Calculated", "Paid", false)]
    [InlineData("Finance", "Submitted", "Paid", false)]
    [InlineData("Finance", "Locked", "Paid", true)]
    [InlineData("Employee", "Locked", "Paid", false)]
    [InlineData("HR", "Paid", "Reopen", false)]
    [InlineData("Admin", "Paid", "Reopen", false)]
    [InlineData("Finance", "Reviewed", "Submit", true)]
    [InlineData("General Secretary", "Reviewed", "Approve", false)]
    public void ApprovalStagesEnforceRoleBoundaries(string role, string stage, string action, bool allowed) =>
        Assert.Equal(allowed, PayrollWorkflow.CanDecide(Actor(role), stage, action));
    [Theory]
    [InlineData("Finance")]
    [InlineData("General Secretary")]
    public void PayrollRoleDoesNotExpandEmployeeAccessOnOtherPages(string role)
    {
        var http = new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        http.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Employee"), new Claim(ClaimTypes.Role, role)], "test"));
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<HrSystemDhg.Web.Data.HrDbContext>().Options;
        using var db = new HrSystemDhg.Web.Data.HrDbContext(options, http);
        http.HttpContext.Request.RouteValues["area"] = "Management";
        http.HttpContext.Request.RouteValues["page"] = "/Payroll";
        Assert.False(db.EmployeeOnly);
        http.HttpContext.Request.RouteValues["area"] = "";
        http.HttpContext.Request.RouteValues["page"] = "/Me/Profile";
        Assert.True(db.EmployeeOnly);
    }

}
