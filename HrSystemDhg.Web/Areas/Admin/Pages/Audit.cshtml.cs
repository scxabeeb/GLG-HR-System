using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class AuditModel(HrDbContext db) : PageModel
{
    public List<AuditLog> Items = [];
    public async Task OnGetAsync() => Items = await db.AuditLogs.OrderByDescending(x => x.Id).Take(300).ToListAsync();
}
