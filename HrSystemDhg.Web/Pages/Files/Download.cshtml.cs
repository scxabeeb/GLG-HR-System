using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Files;
[Authorize(Roles = "Employee,HR,Admin,Management")]
public class DownloadModel(HrDbContext db, DocumentService documents) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id, bool inline = false)
    {
        var d = await db.EmployeeDocuments.SingleOrDefaultAsync(x => x.Id == id);
        if (d == null)
            return NotFound();
        if (!User.IsInRole("HR") && !User.IsInRole("Admin"))
        {
            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var own = await db.Users.AnyAsync(x => x.Id == uid && x.EmployeeId == d.EmployeeId);
            if (!own && !(User.IsInRole("Management") && await db.LeaveRequests.AnyAsync(x => x.DocumentId == id)))
                return Forbid();
        }

        var path = Path.Combine(documents.Root, Path.GetFileName(d.StoragePath));
        if (!System.IO.File.Exists(path))
            return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (inline && d.ContentType.StartsWith("image/"))
            return PhysicalFile(path, d.ContentType);
        return PhysicalFile(path, d.ContentType, d.OriginalName);
    }
}
