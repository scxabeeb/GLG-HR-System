using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles="Admin")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class DataResetModel(EmployeeDataReset reset) : PageModel
{
    public EmployeeDataReset.Preview Counts {get;set;} = null!;
    [BindProperty(SupportsGet=true)] public Guid? BackupId {get;set;}
    [BindProperty] public string Confirmation {get;set;}="";
    string Actor => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
    public async Task OnGetAsync() => Counts=await reset.PreviewAsync();
    public async Task<IActionResult> OnPostBackupAsync()
    {
        var backup=await reset.CreateBackupAsync(Actor);
        return RedirectToPage(new{BackupId=backup.Id});
    }
    public async Task<IActionResult> OnGetDownloadAsync(Guid id)
    {
        var backup=await reset.ReadBackupAsync(id);
        if(backup==null)return NotFound();
        return new JsonResult(backup){ContentType="application/json",StatusCode=200};
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if(Confirmation!="RESET EMPLOYEE DATA"||BackupId==null)ModelState.AddModelError("","Create a backup and type RESET EMPLOYEE DATA exactly.");
        if(!ModelState.IsValid){await OnGetAsync();return Page();}
        try{await reset.ResetAsync(BackupId!.Value,Actor);}
        catch(InvalidOperationException e){ModelState.AddModelError("",e.Message);await OnGetAsync();return Page();}
        TempData["Success"]="Employee, attendance, salary, payroll and leave records cleared. Other login accounts removed; Admin and General Secretary accounts and system configuration retained.";
        return RedirectToPage();
    }
}
