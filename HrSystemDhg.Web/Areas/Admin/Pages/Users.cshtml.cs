using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class UsersModel(HrDbContext db, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> users) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    public List<Employee> Employees = [];
    public string? CurrentUserId => Own;
    public List<ApplicationUser> Items = [];
    public List<Branch> Branches = [];
    public List<string> AvailableRoles=[];
    public Dictionary<string,HashSet<string>> AssignedRoles=[];
    public Dictionary<string,HashSet<string>> InheritedDenied=[];
    public Dictionary<string,Dictionary<string,string>> UserAccess=[];
    public Dictionary<string,Employee> LinkedEmployees=[];
    public Dictionary<string, HashSet<string>> Restricted { get; private set; } = [];
    public List<UserBranchScope> Scopes = [];
    public Dictionary<string, string> Roles = [];
    public async Task OnGetAsync()
    {
        Items = await db.Users.Where(u => Search == null || Search == "" || (u.Email != null && u.Email.Contains(Search)) || (u.UserName != null && u.UserName.Contains(Search)) || db.Employees.Any(e => e.Id == u.EmployeeId && (e.FullName.Contains(Search) || e.EmployeeNumber.Contains(Search)))).OrderBy(u => u.Email).ToListAsync();
        Employees = await db.Employees.Where(e => !db.Users.Any(u => u.EmployeeId == e.Id)).ToListAsync();
        Branches = await db.Branchs.ToListAsync();
        AvailableRoles=await db.Roles.OrderBy(r=>r.Name).Select(r=>r.Name!).ToListAsync();
        LinkedEmployees=await (from account in db.Users join employee in db.Employees on account.EmployeeId equals employee.Id select new{account.Id,Employee=employee}).ToDictionaryAsync(x=>x.Id,x=>x.Employee);

        Scopes = await db.UserBranchScopes.ToListAsync();
        var ids = Items.Select(u => u.Id).ToArray();
        Restricted = (await db.UserModuleRestrictions.AsNoTracking().Where(r => ids.Contains(r.UserId)).ToListAsync())
            .GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.Select(r => r.Module).ToHashSet());
        foreach (var u in Items)
        {
            var assigned = await users.GetRolesAsync(u);
            AssignedRoles[u.Id]=assigned.ToHashSet();
            InheritedDenied[u.Id]=await new RoleAccess(db).RoleDeniedAsync(u.Id);
            var allowed=await db.UserClaims.AsNoTracking().Where(c=>c.UserId==u.Id && c.ClaimType==RoleAccess.AllowedMenuClaim).Select(c=>c.ClaimValue!).ToListAsync();
            UserAccess[u.Id]=RoleAccess.Keys.ToDictionary(key=>key,key=>Restricted.GetValueOrDefault(u.Id)?.Contains(key)==true?"Deny":allowed.Contains(key)?"Allow":"Inherit");
            Roles[u.Id] = string.Join(", ", assigned);
            if (assigned.Contains("General Secretary") && !assigned.Contains("Management"))
                Roles[u.Id] += ", Management (included with General Secretary)";
        }
    }

    private string Actor => User.Identity!.Name!;
    private string? Own => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private async Task Log(string action)
    {
        db.AuditLogs.Add(new() { Actor = Actor, Action = action });
        await db.SaveChangesAsync();
    }

    public async Task<IActionResult> OnPostRoleAsync(string id, string role, bool remove)
    {
        role=await db.Roles.Where(r=>r.NormalizedName==users.NormalizeName(role)).Select(r=>r.Name!).FirstOrDefaultAsync()??"";
        if (role=="" || id==Own && remove && role=="Admin")return BadRequest();
        var u = await users.FindByIdAsync(id);
        if (u == null)
            return NotFound();
        if (await users.IsInRoleAsync(u, role) == !remove)
            return RedirectToPage();
        await using var transaction=await db.Database.BeginTransactionAsync();
        var result = remove ? await users.RemoveFromRoleAsync(u, role) : await users.AddToRoleAsync(u, role);
        if (!result.Succeeded)
            return BadRequest();
        if(!(await users.UpdateSecurityStampAsync(u)).Succeeded)return BadRequest("Unable to refresh account sessions.");
        await Log($"User {id}: {(remove ? "removed" : "added")} role {role}");await transaction.CommitAsync();
        TempData["Success"] = "Role permissions updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRolesAsync(string id,string[] roles)
    {
        var selected=roles.Distinct().ToHashSet();
        var available=await db.Roles.Select(r=>r.Name!).ToListAsync();
        if(!ModelState.IsValid || selected.Any(r=>!available.Contains(r)) || id==Own && !selected.Contains("Admin"))return BadRequest("Choose existing roles and retain your own Admin role.");
        var account=await users.FindByIdAsync(id);if(account==null)return NotFound();
        await using var transaction=await db.Database.BeginTransactionAsync();
        var current=await users.GetRolesAsync(account);
        var removed=await users.RemoveFromRolesAsync(account,current.Except(selected));
        var added=removed.Succeeded?await users.AddToRolesAsync(account,selected.Except(current)):removed;
        if(!added.Succeeded)return BadRequest("Unable to update roles.");
        if(!(await users.UpdateSecurityStampAsync(account)).Succeeded)return BadRequest("Unable to refresh account sessions.");
        await Log($"User {id}: assigned roles {string.Join(", ",selected.Order())}");await transaction.CommitAsync();
        TempData["Success"]="Roles saved. The employee profile is unchanged. The user should sign in again.";
        return RedirectToPage(new{Search});
    }
    public async Task<IActionResult> OnPostResetPasswordAsync(string id,string? password,string? confirm)
    {
        if(id==Own)return BadRequest("Use My profile / Change password for your own account.");
        if(string.IsNullOrEmpty(password) || password.Length is <4 or >128 || password!=confirm)
        {ModelState.AddModelError("","Enter matching temporary passwords of 4–128 characters.");await OnGetAsync();return Page();}
        var account=await users.FindByIdAsync(id);if(account==null)return NotFound();
        await using var transaction=await db.Database.BeginTransactionAsync();
        var token=await users.GeneratePasswordResetTokenAsync(account);
        var result=await users.ResetPasswordAsync(account,token,password);
        if(!result.Succeeded){foreach(var error in result.Errors)ModelState.AddModelError("",error.Description);await OnGetAsync();return Page();}
        if(account.LockoutEnabled)
        {
            if(!(await users.SetLockoutEndDateAsync(account,null)).Succeeded || !(await users.ResetAccessFailedCountAsync(account)).Succeeded)
                return BadRequest("Unable to clear this account's lockout.");
        }
        account.MustChangePassword=true;
        if(!(await users.UpdateAsync(account)).Succeeded)return BadRequest("Unable to update this account.");
        if(!(await users.UpdateSecurityStampAsync(account)).Succeeded)return BadRequest("Unable to revoke existing sessions.");
        await Log($"Administrator reset password for user {id}; change required at next sign-in");await transaction.CommitAsync();
        TempData["Success"]="Temporary password saved. Existing web/mobile sessions are invalid and the user must change it after signing in.";
        return RedirectToPage(new{Search});
    }

    public async Task<IActionResult> OnPostToggleAsync(string id)
    {
        if (id == Own)
            return BadRequest("You cannot disable your own account.");
        var u = await users.FindByIdAsync(id);
        if (u == null)
            return NotFound();
        await using var transaction=await db.Database.BeginTransactionAsync();
        u.IsEnabled = !u.IsEnabled;
        var result=await users.UpdateAsync(u);if(!result.Succeeded)return BadRequest("Unable to update this account.");
        if(!(await users.UpdateSecurityStampAsync(u)).Succeeded)return BadRequest("Unable to refresh account sessions.");
        await Log($"User {id}: enabled={u.IsEnabled}");await transaction.CommitAsync();
        TempData["Success"]=u.IsEnabled?"Account is active.":"Account is inactive; existing sessions are blocked.";
        return RedirectToPage(new{Search});
    }

    public async Task<IActionResult> OnPostRevokeAsync(string id)
    {
        if (id == Own) return BadRequest("You cannot revoke your own administrator access.");
        var u = await users.FindByIdAsync(id);
        if (u == null) return NotFound();
        await using var transaction=await db.Database.BeginTransactionAsync();
        var result = await users.RemoveFromRolesAsync(u, await users.GetRolesAsync(u));
        if (!result.Succeeded) return BadRequest("Unable to revoke roles.");
        if(!(await users.UpdateSecurityStampAsync(u)).Succeeded)return BadRequest("Unable to refresh account sessions.");
        await Log($"Revoked all roles for user {id}");await transaction.CommitAsync();
        TempData["Success"] = "All role permissions revoked. Disable the account to prevent sign-in entirely.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostModulesAsync(string id, string[] blocked)
    {
        if (!ModelState.IsValid || id == Own || blocked.Any(b => !ModuleAccess.Modules.Any(m => m.Key == b)))
            return BadRequest();
        var user = await users.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (await users.IsInRoleAsync(user, "Admin")) return BadRequest("Administrator accounts retain full module access.");
        var selected = blocked.ToHashSet();
        var moduleKeys = ModuleAccess.Modules.Select(m => m.Key).ToArray();
        db.UserClaims.RemoveRange(await db.UserClaims.Where(c=>c.UserId==id && c.ClaimType==RoleAccess.AllowedMenuClaim && moduleKeys.Contains(c.ClaimValue!)).ToListAsync());
        var previous = await db.UserModuleRestrictions.Where(r => r.UserId == id && moduleKeys.Contains(r.Module)).ToListAsync();
        db.UserModuleRestrictions.RemoveRange(previous.Where(r => !selected.Contains(r.Module)));
        foreach (var module in selected.Except(previous.Select(r => r.Module)))
            db.UserModuleRestrictions.Add(new() { UserId = id, Module = module });
        await Log($"User {id}: module restrictions set to {(selected.Count == 0 ? "none" : string.Join(", ", selected.Order()))}");
        TempData["Success"] = "Module access updated. Changes take effect on the user’s next request.";
        return RedirectToPage(new { Search });
    }

    public async Task<IActionResult> OnPostMenuAccessAsync(string id, Dictionary<string, string> access)
    {
        var keys = ModuleAccess.Modules.Select(m => m.Key).Concat(MenuPermissions.Items.Select(m => m.Key)).ToHashSet();
        if (!ModelState.IsValid || id == Own || access.Count != keys.Count || access.Any(x => !keys.Contains(x.Key) || x.Value is not ("Inherit" or "Allow" or "Deny")))
            return BadRequest("Submit Inherit, Allow or Deny for every menu and submenu.");
        var user = await users.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (await users.IsInRoleAsync(user, "Admin")) return BadRequest("Administrator accounts retain full access.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var selected = access.Where(x => x.Value == "Deny").Select(x => x.Key).ToHashSet();
        var overrides=await db.UserClaims.Where(c=>c.UserId==id && c.ClaimType==RoleAccess.AllowedMenuClaim).ToListAsync();
        db.UserClaims.RemoveRange(overrides);
        foreach(var key in access.Where(a=>a.Value=="Allow").Select(a=>a.Key))
            db.UserClaims.Add(new(){UserId=id,ClaimType=RoleAccess.AllowedMenuClaim,ClaimValue=key});
        var previous = await db.UserModuleRestrictions.Where(r => r.UserId == id).ToListAsync();
        db.UserModuleRestrictions.RemoveRange(previous.Where(r => keys.Contains(r.Module) && !selected.Contains(r.Module)));
        foreach (var key in selected.Except(previous.Select(r => r.Module)))
            db.UserModuleRestrictions.Add(new() { UserId = id, Module = key });
        await Log($"User {id}: menu/submenu denied: {(selected.Count == 0 ? "none" : string.Join(", ",selected.Order()))}");
        await transaction.CommitAsync();
        TempData["Success"] = "Menu permissions saved. Denied pages and actions are blocked on the next request, including mobile access.";
        return RedirectToPage(new { Search });
    }

    public async Task<IActionResult> OnPostScopeAsync(string id, int[] branches)
    {
        if (id == Own)
            return BadRequest();
        if (!await db.Users.AnyAsync(x => x.Id == id) || branches.Any(b => !db.Branchs.Any(x => x.Id == b)))
            return BadRequest();
        await using var transaction = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync() : null;
        db.UserBranchScopes.RemoveRange(await db.UserBranchScopes.Where(x => x.UserId == id).ToListAsync());
        await db.SaveChangesAsync();
        foreach (var b in branches.Distinct())
            db.UserBranchScopes.Add(new() { UserId = id, BranchId = b });
        await Log("Updated branch scope for user " + id);
        if (transaction != null) await transaction.CommitAsync();
        TempData["Success"] = "Branch access updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateAsync(string? email, string password, string role, int? employeeId, string? username = null)
    {
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        username = string.IsNullOrWhiteSpace(username) ? email : username.Trim();
        if (string.IsNullOrEmpty(username) || username.Length > 256)
            ModelState.AddModelError("", "Enter a username or email address, up to 256 characters.");
        if (email == null && username != null && new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(username))
            email = username;
        if (email != null && (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || email.Length > 256))
            ModelState.AddModelError("", "Enter a valid email address, or leave email blank and use a username.");
        if (string.IsNullOrEmpty(password) || password.Length is < 4 or > 128)
            ModelState.AddModelError("", "Enter a temporary password of 4–128 characters.");
        role=await db.Roles.Where(r=>r.NormalizedName==users.NormalizeName(role)).Select(r=>r.Name!).FirstOrDefaultAsync()??"";
        if(role=="")ModelState.AddModelError("", "Select a valid role.");
        if (employeeId != null && (!await db.Employees.AnyAsync(x => x.Id == employeeId) || await db.Users.AnyAsync(x => x.EmployeeId == employeeId)))
            ModelState.AddModelError("", "Select an employee who does not already have a login account.");
        var identifiers = new[] { users.NormalizeName(username), users.NormalizeEmail(email) }.Where(x => !string.IsNullOrEmpty(x)).ToArray();
        if (await db.Users.AnyAsync(x => identifiers.Contains(x.NormalizedUserName) || identifiers.Contains(x.NormalizedEmail)))
            ModelState.AddModelError("", "An account already uses this username or email address.");
        if (username != null && await db.Employees.AnyAsync(e => e.EmployeeNumber == username && (employeeId == null || e.Id != employeeId)))
            ModelState.AddModelError("", "This username is reserved for an existing employee. Link that employee or choose another username.");
        if (!ModelState.IsValid) { await OnGetAsync(); return Page(); }
        await using var transaction = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted) : null;
        if (employeeId.HasValue)
        {
            var profiles = await db.Employees.FromSqlInterpolated($"SELECT * FROM Employees WHERE Id = {employeeId.Value} FOR UPDATE").IgnoreQueryFilters().ToListAsync();
            if (profiles.Count == 0 || await db.Users.AnyAsync(x => x.EmployeeId == employeeId.Value))
            {
                ModelState.AddModelError("", "This employee already has a login account. Select another employee or leave the profile unlinked.");
                await OnGetAsync();
                return Page();
            }
        }
        var u = new ApplicationUser
        {
            UserName = username,
            Email = email,
            EmployeeId = employeeId,
            MustChangePassword = true
        };
        var result = await users.CreateAsync(u, password);
        if (!result.Succeeded)
        {
            await OnGetAsync();
            foreach (var e in result.Errors)
                ModelState.AddModelError("", e.Description);
            return Page();
        }

        var roleResult = await users.AddToRoleAsync(u, role);
        if (!roleResult.Succeeded) return BadRequest("Unable to assign the selected role.");
        await Log("Created staff account " + username);
        if (transaction != null) await transaction.CommitAsync();
        TempData["Success"] = "Account created. The user must change their temporary password at first sign-in.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLinkAsync(string id, int employeeId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var accounts = await db.Users.FromSqlInterpolated($"SELECT * FROM AspNetUsers WHERE Id = {id} FOR UPDATE").ToListAsync();
        var account = accounts.SingleOrDefault();
        if (account == null) return NotFound();
        if (account.EmployeeId.HasValue) return BadRequest("This account already has an employee profile.");
        var profiles = await db.Employees.FromSqlInterpolated($"SELECT * FROM Employees WHERE Id = {employeeId} FOR UPDATE").IgnoreQueryFilters().ToListAsync();
        if (profiles.Count == 0 || await db.Users.AnyAsync(u => u.EmployeeId == employeeId))
        {
            ModelState.AddModelError("", "Select an available employee who does not already have a login.");
            await OnGetAsync();
            return Page();
        }
        account.EmployeeId = employeeId;
        var result = await users.UpdateSecurityStampAsync(account);
        if (!result.Succeeded) return BadRequest("Unable to link this employee profile.");
        await Log($"User {id}: linked employee profile {employeeId}");
        await transaction.CommitAsync();
        TempData["Success"] = "Employee profile linked. The user should sign in again to access their personal records.";
        return RedirectToPage(new { Search });
    }

    private const string LegacyCsvColumns = "Email,Role,EmployeeId,TemporaryPassword";
    private const string CsvColumns = LegacyCsvColumns + ",Username";

    public IActionResult OnGetSample()
    {
        return RegistrationCsv.Download("user-accounts-sample", CsvColumns, [["staff@example.com", "Employee", null, null, "staff.user"]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var records = await (from user in db.Users.AsNoTracking()
            join membership in db.UserRoles on user.Id equals membership.UserId
            join role in db.Roles on membership.RoleId equals role.Id
            orderby user.Email, role.Name
            select new { user.Email, user.UserName, Role = role.Name, user.EmployeeId }).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("user-accounts", CsvColumns, records.Select(x => new object?[] { x.Email, x.Role, x.EmployeeId, null, x.UserName }));
    }

    public async Task<IActionResult> OnPostImportAsync(IFormFile? upload)
    {
        var columns = CsvColumns;
        if (upload != null && upload.Length is > 0 and <= 2 * 1024 * 1024)
        {
            using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(upload.OpenReadStream());
            parser.SetDelimiters(",");
            try
            {
                var headers = parser.ReadFields();
                if (headers != null && headers.Length == 4 && headers.ToHashSet().SetEquals(LegacyCsvColumns.Split(','))) columns = LegacyCsvColumns;
            }
            catch (Microsoft.VisualBasic.FileIO.MalformedLineException) { /* The shared importer returns a validation message. */ }
        }
        var includesUsername = columns == CsvColumns;
        return await RegistrationCsv.ImportAsync(this, db, upload, columns, async row =>
        {
            return await OnPostCreateAsync(row.Optional("Email"), row.Get<string>("TemporaryPassword"),
                row.Get<string>("Role"), row.Get<int?>("EmployeeId"), includesUsername ? row.Optional("Username") : null);
        });
    }
}
