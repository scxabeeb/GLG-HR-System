using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class LeaveTypesModel(HrDbContext db) : PageModel
{
    [BindProperty]
    public LeaveType Input { get; set; } = new();

    public List<LeaveType> Types = [];
    public async Task OnGetAsync(int? id)
    {
        Types = await db.LeaveTypes.ToListAsync();
        if (id != null)
            Input = await db.LeaveTypes.SingleAsync(x => x.Id == id);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.Name) || Input.AnnualDays < 0 || Input.MinDays < 1 || Input.MaxDays < Input.MinDays)
        {
            Types = await db.LeaveTypes.ToListAsync();
            ModelState.AddModelError("", "Enter valid leave limits and name.");
            return Page();
        }

        if (Input.Id == 0)
            db.LeaveTypes.Add(Input);
        else
        {
            var t = await db.LeaveTypes.SingleAsync(x => x.Id == Input.Id);
            db.Entry(t).CurrentValues.SetValues(Input);
        }

        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = "Saved leave type " + Input.Name });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }
}
