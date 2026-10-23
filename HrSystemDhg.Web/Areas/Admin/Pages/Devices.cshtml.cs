using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class DevicesModel(HrDbContext db) : PageModel
{
    public List<ZkDevice> Items = [];
    public async Task OnGetAsync() => Items = await db.ZkDevices.ToListAsync();
    public async Task<IActionResult> OnPostAsync(string name, string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest();
        db.ZkDevices.Add(new() { Name = name, Endpoint = endpoint ?? "Manual import" });
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = "Added attendance device " + name });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }
}
