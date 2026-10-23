using System.ComponentModel.DataAnnotations;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
namespace HrSystemDhg.Web.Pages.Identity.Account;
[AllowAnonymous][EnableRateLimiting("password-reset")]
public class ResetPasswordModel(PasswordRecoveryService recovery,SignInManager<ApplicationUser> signIn):PageModel
{
 [BindProperty(SupportsGet=true),Required,EmailAddress,StringLength(256)]public string Email{get;set;}="";
 [BindProperty(SupportsGet=true),Required,StringLength(4096)]public string Code{get;set;}="";
 [BindProperty,Required,StringLength(128,MinimumLength=10)]public string Password{get;set;}="";
 [BindProperty,Required,Compare(nameof(Password),ErrorMessage="Passwords do not match.")]public string ConfirmPassword{get;set;}="";
 public bool Complete{get;private set;}
 public void OnGet()=>ModelState.Clear();
 public async Task<IActionResult> OnPostAsync(){if(!ModelState.IsValid)return Page();var result=await recovery.ResetAsync(Email,Code,Password);if(!result.Succeeded){foreach(var error in result.Errors)ModelState.AddModelError("",error.Code=="InvalidToken"?"This reset link is invalid or expired. Request another link.":error.Description);return Page();}await signIn.SignOutAsync();Complete=true;ModelState.Clear();return Page();}
}
