using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class SupportModel(HrDbContext db, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> users, Microsoft.AspNetCore.Identity.SignInManager<ApplicationUser> signIn) : PageModel
{
    public List<ApplicationUser> Users = [];
    public async Task OnGetAsync() => Users = await db.Users.Where(x => x.IsEnabled).ToListAsync();
    public async Task<IActionResult> OnPostAsync(string id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest();
        var own = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var target = await users.FindByIdAsync(id);
        if (target == null || !target.IsEnabled || id == own)
            return BadRequest();
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Started read-only support as {target.Email}: {reason}" });
        await db.SaveChangesAsync();
        await signIn.SignInWithClaimsAsync(target, new Microsoft.AspNetCore.Authentication.AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15), AllowRefresh = false }, new[] { new System.Security.Claims.Claim("support-admin", own!), new System.Security.Claims.Claim("support-expires", DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds().ToString()) });
        return Redirect("/");
    }
}
