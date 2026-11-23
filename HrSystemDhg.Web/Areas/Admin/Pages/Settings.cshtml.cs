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
            { ModelState.AddModelError("", "Enter an organization name up to 200 characters."); return Page(); }
        var s = await db.Settings.FirstOrDefaultAsync();
        if (new[] { Input.PortalName, Input.ShortName, Input.PortalSubtitle }.Any(string.IsNullOrWhiteSpace) ||
            Input.PortalName.Length > 160 || Input.ShortName.Length > 60 || Input.PortalSubtitle.Length > 80)
        { ModelState.AddModelError("", "Enter valid portal name, short name and subtitle."); return Page(); }
        if (s == null) { s = new(); db.Settings.Add(s); }
        s.CompanyName = Input.CompanyName.Trim();
        s.PortalName = Input.PortalName.Trim(); s.ShortName = Input.ShortName.Trim(); s.PortalSubtitle = Input.PortalSubtitle.Trim();
        s.AllowFutureHires = Input.AllowFutureHires; s.ManagementBankAccess = Input.ManagementBankAccess;
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = "Updated system settings" });
        await db.SaveChangesAsync();
        TempData["Success"] = "System preferences saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLogoAsync(IFormFile? logo, bool remove = false)
    {
        var settings = await db.Settings.FirstOrDefaultAsync();
        if (settings == null) { settings = new(); db.Settings.Add(settings); }
        if (remove) { settings.Logo = null; settings.LogoContentType = null; }
        else
        {
            if (logo == null || logo.Length < 24 || logo.Length > 1024 * 1024) return BadRequest("Upload a PNG or JPEG image up to 1 MB.");
            using var stream = new MemoryStream(); await logo.CopyToAsync(stream);
            var bytes = stream.ToArray();
            var png = bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var jpeg = bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217;
            if (!png && !jpeg) return BadRequest("Use a PNG or JPEG image.");
            settings.Logo = bytes; settings.LogoContentType = png ? "image/png" : "image/jpeg";
        }
        settings.LogoUpdatedUtc = DateTime.UtcNow;
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = remove ? "Reset organization logo" : "Updated organization logo" });
        await db.SaveChangesAsync(); TempData["Success"] = "Logo updated.";
        return RedirectToPage(null, null, "settings-general");
    }
}
