using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HrSystemDhg.Web.Models;

namespace HrSystemDhg.Web.Pages.Identity.Account;
public class LogoutModel(SignInManager<ApplicationUser> signIn) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await signIn.SignOutAsync();
        return RedirectToPage("Login");
    }
}
