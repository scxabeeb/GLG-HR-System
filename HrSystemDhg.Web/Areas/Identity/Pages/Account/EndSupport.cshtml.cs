using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Identity.Account;
[Authorize(Roles = "Employee,HR,Management,Admin")]
public class EndSupportModel(HrDbContext db, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> users, Microsoft.AspNetCore.Identity.SignInManager<ApplicationUser> signIn) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        var original = User.FindFirst("support-admin")?.Value;
        if (original == null)
            return BadRequest();
        var admin = await users.FindByIdAsync(original);
        if (admin == null || !admin.IsEnabled || !await users.IsInRoleAsync(admin, "Admin"))
        {
            await signIn.SignOutAsync();
            return RedirectToPage("Login");
        }

        db.AuditLogs.Add(new() { Actor = admin.Email!, Action = "Ended read-only support session" });
        await db.SaveChangesAsync();
        await signIn.SignInAsync(admin, false);
        return Redirect("/");
    }
}
