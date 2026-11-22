using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HrSystemDhg.Web.Pages.Me;
[Authorize(Roles = "Employee,HR,Management,Admin")]
public class ApprovalsModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Profile");
    public IActionResult OnPost() => RedirectToPage("/Profile");
}
