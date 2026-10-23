using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Hr;
[Authorize(Roles = "HR,Management,Admin")]
public class LeaveModel(HrDbContext db, LeaveService leaves) : PageModel
{
    public List<LeaveRequest> Items = [];
    public Dictionary<int, string> Names = [];
    public Dictionary<int, string> Types = [];
    public async Task OnGetAsync()
    {
        Items = await db.LeaveRequests.OrderByDescending(x => x.Id).ToListAsync();
        Names = await db.Employees.ToDictionaryAsync(x => x.Id, x => x.FullName);
        Types = await db.LeaveTypes.ToDictionaryAsync(x => x.Id, x => x.Name);
    }

    public async Task<IActionResult> OnPostAsync(int id, string decision, string? reason)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var approver = await db.Users.Where(x => x.Id == userId).Select(x => x.EmployeeId).SingleAsync();
            await leaves.DecideAsync(id, decision, reason ?? "", User.Identity!.Name!, approver, User.IsInRole("Management") || User.IsInRole("Admin"));
            return RedirectToPage();
        }
        catch (InvalidOperationException e)
        {
            ModelState.AddModelError("", e.Message);
            await OnGetAsync();
            return Page();
        }
    }
}
