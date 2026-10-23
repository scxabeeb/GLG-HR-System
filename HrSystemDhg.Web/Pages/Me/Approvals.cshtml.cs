using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Me;
[Authorize(Roles = "Employee,HR,Management,Admin")]
public class ApprovalsModel(HrDbContext db) : PageModel
{
    public List<LeaveRequest> Items = [];
    public Dictionary<int, string> Names = [];
    private async Task<int?> Own()
    {
        var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return await db.Users.Where(x => x.Id == uid).Select(x => x.EmployeeId).SingleAsync();
    }

    public async Task OnGetAsync()
    {
        var own = await Own();
        if (own == null)
            return;
        var reports = await db.Employees.IgnoreQueryFilters().Where(x => x.ManagerId == own).ToListAsync();
        Names = reports.ToDictionary(x => x.Id, x => x.FullName);
        var ids = Names.Keys.ToList();
        Items = await db.LeaveRequests.IgnoreQueryFilters().Where(x => ids.Contains(x.EmployeeId) && x.Status == "Pending").ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync(int id, bool approve, string? reason)
    {
        var own = await Own();
        if (own == null)
            return Forbid();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var r = await db.LeaveRequests.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id && x.Status == "Pending");
        if (r == null || !await db.Employees.IgnoreQueryFilters().AnyAsync(x => x.Id == r.EmployeeId && x.ManagerId == own))
            return NotFound();
        if (!approve && string.IsNullOrWhiteSpace(reason))
            return BadRequest("A rejection reason is required.");
        r.Status = approve ? "Manager approved" : "Rejected";
        r.DecisionReason = reason;
        r.DecidedBy = User.Identity!.Name;
        r.DecidedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Manager decision for leave {id}: {r.Status}" });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return RedirectToPage();
    }
}
