using System.ComponentModel.DataAnnotations;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
namespace HrSystemDhg.Web.Pages.Identity.Account;
[AllowAnonymous][EnableRateLimiting("password-reset")]
public class ForgotPasswordModel(PasswordRecoveryService recovery,IWebHostEnvironment env,IConfiguration config):PageModel
{
 [BindProperty,Required,EmailAddress,StringLength(256)]public string Email{get;set;}="";
 public bool Submitted{get;private set;}
 public bool LocalDelivery=>env.IsDevelopment()&&string.IsNullOrWhiteSpace(config["Smtp:Host"]);
 public async Task<IActionResult> OnPostAsync(){if(!ModelState.IsValid)return Page();await recovery.RequestAsync(Email);Submitted=true;ModelState.Clear();return Page();}
}
