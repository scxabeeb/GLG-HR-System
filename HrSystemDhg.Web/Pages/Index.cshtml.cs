using HrSystemDhg.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages;
public class IndexModel(HrDbContext db) : PageModel
{
    public int Employees, Leave, Branches, Present, Expected;
    public decimal AttendancePercent;
    public Dictionary<string, int> Headcount = [];
    public Dictionary<string, decimal> Costs = [];
    public async Task<IActionResult> OnGetAsync()
    {
        if (Request.Path == "/")
        {
            if (User.IsInRole("Admin"))
                return Redirect("/Admin/Dashboard");
            if (User.IsInRole("Management"))
                return Redirect("/Management/Dashboard");
            if (User.IsInRole("HR"))
                return Redirect("/Hr/Dashboard");
        }

        if (!User.IsInRole("Admin") && !User.IsInRole("HR") && !User.IsInRole("Management"))
            return RedirectToPage("/Me/Profile");
        Employees = await db.Employees.CountAsync(x => x.Status == "Active" || x.Status == "Probation");
        Leave = await db.LeaveRequests.CountAsync(x => x.Status == "Pending" || x.Status == "Manager approved");
        Branches = await db.Branchs.CountAsync(x => x.IsActive);
        var today = HrSystemDhg.Web.Services.BusinessClock.Today;
        Expected = await db.Employees.CountAsync(x => x.HireDate <= today && (x.TerminationDate == null || x.TerminationDate >= today));
        Present = await db.AttendanceDays.CountAsync(x => x.Date == today && (x.Status == "Present" || x.Status == "Late" || x.Status == "On leave"));
        AttendancePercent = Expected == 0 ? 0 : 100m * Present / Expected;
        var assignments = await db.EmployeeAssignments.Where(x => x.EffectiveFrom <= today && (x.EffectiveTo == null || x.EffectiveTo >= today)).ToListAsync();
        foreach (var b in await db.Branchs.ToListAsync())
            Headcount[b.Name] = assignments.Count(x => x.BranchId == b.Id);
        var period = await db.PayrollPeriods.Where(x => x.Status != "Draft").OrderByDescending(x => x.To).FirstOrDefaultAsync();
        if (period != null)
        {
            var lines = await db.PayrollLines.Where(x => x.PayrollPeriodId == period.Id && x.Revision == period.Revision).ToListAsync();
            Costs = lines.GroupBy(x => x.Currency).ToDictionary(x => x.Key, x => x.Sum(l => l.Net));
        }

        return Page();
    }
}
