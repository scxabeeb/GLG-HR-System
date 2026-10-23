using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class SettingsModel(HrDbContext db) : PageModel
{
    [BindProperty]
    public SystemSettings Input { get; set; } = new();

    public async Task OnGetAsync() => Input = await db.Settings.FirstOrDefaultAsync() ?? new();
    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.CompanyName) || Input.CompanyName.Length > 200)
            return BadRequest();
        var s = await db.Settings.FirstOrDefaultAsync();
        Input.Id = 1;
        if (s == null)
            db.Settings.Add(Input);
        else
            db.Entry(s).CurrentValues.SetValues(Input);
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = "Updated system settings" });
        await db.SaveChangesAsync();
        TempData["Success"] = "System preferences saved.";
        return RedirectToPage();
    }
}
