using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Hr;
[Authorize(Roles = "HR,Management,Admin")]
public class CorrectionsModel(HrDbContext db) : PageModel
{
    public List<CorrectionRequest> Items = [];
    public Dictionary<int, string> Names = [];
    public async Task OnGetAsync()
    {
        Items = await db.CorrectionRequests.OrderByDescending(x => x.Id).ToListAsync();
        Names = await db.Employees.ToDictionaryAsync(x => x.Id, x => x.FullName);
    }

    public async Task<IActionResult> OnPostAsync(int id, string resolution, string status)
    {
        if (status is not ("Resolved" or "Rejected") || string.IsNullOrWhiteSpace(resolution))
            return BadRequest();
        var c = await db.CorrectionRequests.SingleAsync(x => x.Id == id);
        c.Status = status;
        c.Resolution = resolution;
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Correction {id}: {status}; {resolution}" });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }
}
