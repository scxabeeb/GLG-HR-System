using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Identity.Account;
[AllowAnonymous]
public class LoginModel(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, HrDbContext db) : PageModel
{
    [BindProperty]
    public string Username { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await users.FindByNameAsync(Username) ?? await users.FindByEmailAsync(Username);
        if (user == null)
        {
            var e = await db.Employees.SingleOrDefaultAsync(x => x.EmployeeNumber == Username);
            if (e != null)
                user = await users.Users.Where(x => x.EmployeeId == e.Id).OrderByDescending(x => x.Email == e.WorkEmail).FirstOrDefaultAsync();
        }

        if (user != null && user.IsEnabled && (await signIn.PasswordSignInAsync(user, Password, false, true)).Succeeded)
            return LocalRedirect("/");
        ModelState.AddModelError("", "Sign in failed. Check your details or try later.");
        return Page();
    }
}
