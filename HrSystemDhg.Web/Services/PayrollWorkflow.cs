using System.Security.Claims;
namespace HrSystemDhg.Web.Services;

public static class PayrollWorkflow
{
    public static bool Generate(ClaimsPrincipal user) => user.IsInRole("HR") || user.IsInRole("Admin");
    public static bool Review(ClaimsPrincipal user) => user.IsInRole("Finance") || user.IsInRole("Admin");
    public static bool Approve(ClaimsPrincipal user) => user.IsInRole("General Secretary");
    public static bool CanDecide(ClaimsPrincipal user, string status, string action) => action switch
    {
        "Submit" => Review(user) && status is ("Calculated" or "Reviewed"),
        "Approve" => Approve(user) && status == "Submitted",
        "Reject" => status == "Submitted" ? Approve(user) : status is ("Calculated" or "Reviewed") && Review(user),
        "Reopen" => Generate(user) && status is ("Calculated" or "Reviewed" or "Submitted" or "Locked"),
        "Paid" => Review(user) && status is ("Locked" or "Paid"),
        _ => false
    };
    public static string Label(string status) => status switch
    {
        "Draft" => "HR preparation", "Calculated" => "Awaiting Finance / Admin review",
        "Reviewed" => "Reviewed — ready to submit", "Submitted" => "Awaiting General Secretary approval",
        "Locked" => "Approved", "Paid" => "Paid", _ => status
    };
}
