using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HrSystemDhg.Web.Models;

namespace HrSystemDhg.Web.Pages.Identity.Account;
public class ChangePasswordModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : PageModel
{
    [BindProperty]
    public string Current { get; set; } = "";

    [BindProperty]
    public string NewPassword { get; set; } = "";

    [BindProperty,Required,Compare(nameof(NewPassword),ErrorMessage="Passwords do not match.")]
    public string ConfirmPassword{get;set;}="";
    public async Task<IActionResult> OnPostAsync()
    {
        if(!ModelState.IsValid)return Page();
        var user = (await users.GetUserAsync(User))!;
        var result = await users.ChangePasswordAsync(user, Current, NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError("", e.Description);
            return Page();
        }

        user.MustChangePassword = false;
        await users.UpdateAsync(user);
        await signIn.RefreshSignInAsync(user);
        return Redirect("/");
    }
}
