using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Files;
[Authorize(Roles = "Employee,HR,Admin,Management,General Secretary")]
public class DownloadModel(HrDbContext db, DocumentService documents) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id, bool inline = false)
    {
        var executive = User.IsInRole("General Secretary");
        var executiveDocument = executive && !((await HttpContext.RequestServices.GetRequiredService<ModuleAccess>().BlockedAsync()).Contains("leave"))
            && await db.LeaveRequests.IgnoreQueryFilters().AnyAsync(r => r.DocumentId == id
                && (r.Status == LeaveService.Submitted || r.Status == "Approved" || r.Status == "Rejected")
                && (!db.Scoped || db.EmployeeAssignments.IgnoreQueryFilters().Any(a => a.EmployeeId == r.EmployeeId
                    && db.AllowedBranchIds.Contains(a.BranchId) && a.EffectiveFrom <= db.ScopeDate
                    && (a.EffectiveTo == null || a.EffectiveTo >= db.ScopeDate))));
        var source = executiveDocument ? db.EmployeeDocuments.IgnoreQueryFilters() : db.EmployeeDocuments;
        var d = await source.SingleOrDefaultAsync(x => x.Id == id);
        if (d == null)
            return NotFound();
        if (!User.IsInRole("HR") && !User.IsInRole("Admin"))
        {
            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var own = await db.Users.AnyAsync(x => x.Id == uid && x.EmployeeId == d.EmployeeId);
            if (!own && !executiveDocument && !(User.IsInRole("Management") && await db.LeaveRequests.AnyAsync(x => x.DocumentId == id)))
                return Forbid();
        }

        var path = Path.Combine(documents.Root, Path.GetFileName(d.StoragePath));
        if (!System.IO.File.Exists(path))
            return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        if (inline && d.ContentType is ("application/pdf" or "image/jpeg" or "image/png"))
        {
            Response.Headers["Content-Security-Policy"] = "sandbox";
            return new PhysicalFileResult(path, d.ContentType) { EnableRangeProcessing = true };
        }
        return PhysicalFile(path, d.ContentType, d.OriginalName);
    }
}
