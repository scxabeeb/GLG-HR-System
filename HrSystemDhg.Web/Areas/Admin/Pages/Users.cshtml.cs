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
    public Dictionary<string, HashSet<string>> Restricted { get; private set; } = [];
    public List<UserBranchScope> Scopes = [];
    public Dictionary<string, string> Roles = [];
    public async Task OnGetAsync()
    {
        Items = await db.Users.Where(u => Search == null || Search == "" || (u.Email != null && u.Email.Contains(Search)) || (u.UserName != null && u.UserName.Contains(Search)) || db.Employees.Any(e => e.Id == u.EmployeeId && (e.FullName.Contains(Search) || e.EmployeeNumber.Contains(Search)))).OrderBy(u => u.Email).ToListAsync();
        Employees = await db.Employees.Where(e => !db.Users.Any(u => u.EmployeeId == e.Id)).ToListAsync();
        Branches = await db.Branchs.ToListAsync();
        Scopes = await db.UserBranchScopes.ToListAsync();
        var ids = Items.Select(u => u.Id).ToArray();
        Restricted = (await db.UserModuleRestrictions.AsNoTracking().Where(r => ids.Contains(r.UserId)).ToListAsync())
            .GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.Select(r => r.Module).ToHashSet());
        foreach (var u in Items)
        {
            var assigned = await users.GetRolesAsync(u);
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
        if (!new[]
        {
            "Admin",
            "Management",
            "Finance",
            "HR",
            "General Secretary",
            "Chairman",
            "Employee"
        }.Contains(role) || id == Own && remove && role == "Admin")
            return BadRequest();
        var u = await users.FindByIdAsync(id);
        if (u == null)
            return NotFound();
        if (await users.IsInRoleAsync(u, role) == !remove)
            return RedirectToPage();
        var result = remove ? await users.RemoveFromRoleAsync(u, role) : await users.AddToRoleAsync(u, role);
        if (!result.Succeeded)
            return BadRequest();
        await users.UpdateSecurityStampAsync(u);
        await Log($"User {id}: {(remove ? "removed" : "added")} role {role}");
        TempData["Success"] = "Role permissions updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(string id)
    {
        if (id == Own)
            return BadRequest("You cannot disable your own account.");
        var u = await users.FindByIdAsync(id);
        if (u == null)
            return NotFound();
        u.IsEnabled = !u.IsEnabled;
        await users.UpdateAsync(u);
        await users.UpdateSecurityStampAsync(u);
        await Log($"User {id}: enabled={u.IsEnabled}");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokeAsync(string id)
    {
        if (id == Own) return BadRequest("You cannot revoke your own administrator access.");
        var u = await users.FindByIdAsync(id);
        if (u == null) return NotFound();
        var result = await users.RemoveFromRolesAsync(u, await users.GetRolesAsync(u));
        if (!result.Succeeded) return BadRequest("Unable to revoke roles.");
        await users.UpdateSecurityStampAsync(u);
        await Log($"Revoked all roles for user {id}");
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
        if (!ModelState.IsValid || id == Own || access.Count != keys.Count || access.Any(x => !keys.Contains(x.Key) || x.Value is not ("Allow" or "Deny")))
            return BadRequest("Submit a valid choice for every menu and submenu.");
        var user = await users.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (await users.IsInRoleAsync(user, "Admin")) return BadRequest("Administrator accounts retain full access.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var selected = access.Where(x => x.Value == "Deny").Select(x => x.Key).ToHashSet();
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

    public async Task<IActionResult> OnPostCreateAsync(string email, string password, string role, int? employeeId)
    {
        if (string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || email.Length > 256)
            ModelState.AddModelError("", "Enter a valid email address.");
        if (string.IsNullOrEmpty(password) || password.Length is < 10 or > 128)
            ModelState.AddModelError("", "Enter a temporary password of 10–128 characters.");
        if (!new[] { "Admin", "HR", "Management", "Finance", "General Secretary", "Chairman", "Employee" }.Contains(role))
            ModelState.AddModelError("", "Select a valid role.");
        if (employeeId != null && (!await db.Employees.AnyAsync(x => x.Id == employeeId) || await db.Users.AnyAsync(x => x.EmployeeId == employeeId)))
            ModelState.AddModelError("", "Select an employee who does not already have a login account.");
        if (!string.IsNullOrWhiteSpace(email) && await db.Users.AnyAsync(x => x.NormalizedEmail == email.Trim().ToUpperInvariant() || x.NormalizedUserName == email.Trim().ToUpperInvariant()))
            ModelState.AddModelError("", "An account already uses this email address.");
        if (!ModelState.IsValid) { await OnGetAsync(); return Page(); }
        email = email.Trim();
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
            UserName = email,
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
        await Log("Created staff account " + email);
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

    private const string CsvColumns = "Email,Role,EmployeeId,TemporaryPassword";

    public IActionResult OnGetSample()
    {
        return RegistrationCsv.Download("user-accounts-sample", CsvColumns, [["staff@example.com", "Employee", 1, null]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var records = await (from user in db.Users.AsNoTracking()
            join membership in db.UserRoles on user.Id equals membership.UserId
            join role in db.Roles on membership.RoleId equals role.Id
            orderby user.Email, role.Name
            select new { user.Email, Role = role.Name, user.EmployeeId }).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("user-accounts", CsvColumns, records.Select(x => new object?[] { x.Email, x.Role, x.EmployeeId, null }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload)
    {
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            return await OnPostCreateAsync(row.Get<string>("Email"), row.Get<string>("TemporaryPassword"),
                row.Get<string>("Role"), row.Get<int?>("EmployeeId"));
        });
    }
}
