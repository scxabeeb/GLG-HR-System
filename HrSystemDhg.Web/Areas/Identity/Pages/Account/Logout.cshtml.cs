using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HrSystemDhg.Web.Models;

namespace HrSystemDhg.Web.Pages.Identity.Account;
public class LogoutModel(SignInManager<ApplicationUser> signIn) : PageModel
{
    public async Task<IActionResult> OnPostAsync(bool mobile = false)
    {
        await signIn.SignOutAsync();
        return mobile
            ? RedirectToPage("Login", new { returnUrl = "/api/mobile/v1/session" })
            : RedirectToPage("Login");
    }
}
