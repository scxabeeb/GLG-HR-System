using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles="Admin")]
public class RolesModel(HrDbContext db,RoleManager<IdentityRole> roles):PageModel
{
    [BindProperty(SupportsGet=true)] public string? Id {get;set;}
    public List<IdentityRole> Items=[];
    public Dictionary<string,int> Members=[];
    public IdentityRole? Selected {get;private set;}
    public HashSet<string> Denied=[];
    public HashSet<string> Capabilities=[];
    public async Task OnGetAsync()
    {
        Items=await roles.Roles.OrderBy(r=>r.Name).ToListAsync();
        Members=await db.UserRoles.GroupBy(r=>r.RoleId).ToDictionaryAsync(g=>g.Key,g=>g.Count());
        Selected=Items.FirstOrDefault(r=>r.Id==Id);
        if(Selected!=null){var claims=await roles.GetClaimsAsync(Selected);Denied=claims.Where(c=>c.Type==RoleAccess.DeniedMenuClaim).Select(c=>c.Value).ToHashSet();Capabilities=claims.Where(c=>c.Type==RoleAccess.BaseRoleClaim).Select(c=>c.Value).ToHashSet();}
    }
    private async Task<IActionResult> Error(string message){ModelState.AddModelError("",message);await OnGetAsync();return Page();}
    private async Task Audit(string action){db.AuditLogs.Add(new(){Actor=User.Identity!.Name!,Action=action});await db.SaveChangesAsync();}
    public async Task<IActionResult> OnPostCreateAsync(string? name,string[] capabilities)
    {
        name=name?.Trim();
        if(string.IsNullOrWhiteSpace(name)||name.Length>64||name.Any(c=>!char.IsLetterOrDigit(c)&&!" ._-".Contains(c))||RoleAccess.BuiltIn(name))return await Error("Use a unique custom role name of 1–64 letters, digits, spaces, dots, underscores or hyphens. Built-in role names are reserved.");
        if(capabilities.Length==0||capabilities.Any(c=>!RoleAccess.CapabilityRoles.Contains(c)))return await Error("Choose at least one operational capability. Assign the built-in Admin role separately for full administration.");
        await using var transaction=await db.Database.BeginTransactionAsync();
        var role=new IdentityRole(name);var result=await roles.CreateAsync(role);
        if(!result.Succeeded)return await Error(string.Join(" ",result.Errors.Select(e=>e.Description)));
        foreach(var capability in capabilities.Distinct())
        {
            var claim=await roles.AddClaimAsync(role,new(RoleAccess.BaseRoleClaim,capability));
            if(!claim.Succeeded)return await Error("Unable to save role capabilities.");
        }
        await Audit("Created custom role "+name+"; capabilities: "+string.Join(", ",capabilities.Distinct()));await transaction.CommitAsync();
        TempData["RoleNotice"]="Role created. Review its menu and submenu access below.";return RedirectToPage(new{Id=role.Id});
    }
    public async Task<IActionResult> OnPostSaveAsync(string id,string[] capabilities,Dictionary<string,string> access)
    {
        Id=id;var role=await roles.FindByIdAsync(id);if(role==null)return NotFound();
        if(role.Name=="Admin")return BadRequest("Built-in Admin retains full access.");
        if(access.Count!=RoleAccess.Keys.Count||access.Any(a=>!RoleAccess.Keys.Contains(a.Key)||a.Value is not("Allow" or "Deny")))return await Error("Select Allow or Deny for every menu and submenu.");
        if(!RoleAccess.BuiltIn(role.Name)&&(capabilities.Length==0||capabilities.Any(c=>!RoleAccess.CapabilityRoles.Contains(c))))return await Error("Select valid operational capabilities for this custom role.");
        await using var transaction=await db.Database.BeginTransactionAsync();
        var previous=await roles.GetClaimsAsync(role);
        foreach(var claim in previous.Where(c=>c.Type==RoleAccess.DeniedMenuClaim || c.Type==RoleAccess.BaseRoleClaim && !RoleAccess.BuiltIn(role.Name)))
            if(!(await roles.RemoveClaimAsync(role,claim)).Succeeded)return await Error("Unable to update this role.");
        if(!RoleAccess.BuiltIn(role.Name))foreach(var capability in capabilities.Distinct())
            if(!(await roles.AddClaimAsync(role,new(RoleAccess.BaseRoleClaim,capability))).Succeeded)return await Error("Unable to update capabilities.");
        foreach(var key in access.Where(a=>a.Value=="Deny").Select(a=>a.Key))
            if(!(await roles.AddClaimAsync(role,new(RoleAccess.DeniedMenuClaim,key))).Succeeded)return await Error("Unable to update menu permissions.");
        await Audit("Updated role "+role.Name+" menu permissions and capabilities");await transaction.CommitAsync();
        TempData["RoleNotice"]="Role permissions saved. Changes apply to web and mobile requests.";return RedirectToPage(new{Id=id});
    }
    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        Id=id;var role=await roles.FindByIdAsync(id);if(role==null)return NotFound();
        if(RoleAccess.BuiltIn(role.Name))return BadRequest("Built-in roles cannot be deleted.");
        if(await db.UserRoles.AnyAsync(r=>r.RoleId==id))return await Error("Remove this role from its users before deleting it.");
        var result=await roles.DeleteAsync(role);if(!result.Succeeded)return await Error("Unable to delete this role.");
        await Audit("Deleted unused custom role "+role.Name);TempData["RoleNotice"]="Custom role deleted.";return RedirectToPage();
    }
}
