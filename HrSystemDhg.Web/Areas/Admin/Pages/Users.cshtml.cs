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
    public List<UserBranchScope> Scopes = [];
    public Dictionary<string, string> Roles = [];
    public async Task OnGetAsync()
    {
        Items = await db.Users.Where(u => Search == null || Search == "" || (u.Email != null && u.Email.Contains(Search))).OrderBy(u => u.Email).ToListAsync();
        Employees = await db.Employees.Where(e => !db.Users.Any(u => u.EmployeeId == e.Id)).ToListAsync();
        Branches = await db.Branchs.ToListAsync();
        Scopes = await db.UserBranchScopes.ToListAsync();
        foreach (var u in Items)
            Roles[u.Id] = string.Join(", ", await users.GetRolesAsync(u));
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
            "HR",
            "Employee"
        }.Contains(role) || id == Own && remove && role == "Admin")
            return BadRequest();
        var u = await users.FindByIdAsync(id);
        if (u == null)
            return NotFound();
        if (!remove && role != "Admin" && u.EmployeeId == null)
            return BadRequest("Link an employee before assigning an operational role.");
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

    public async Task<IActionResult> OnPostScopeAsync(string id, int[] branches)
    {
        if (id == Own)
            return BadRequest();
        if (!await db.Users.AnyAsync(x => x.Id == id) || branches.Any(b => !db.Branchs.Any(x => x.Id == b)))
            return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.UserBranchScopes.RemoveRange(await db.UserBranchScopes.Where(x => x.UserId == id).ToListAsync());
        await db.SaveChangesAsync();
        foreach (var b in branches.Distinct())
            db.UserBranchScopes.Add(new() { UserId = id, BranchId = b });
        await Log("Updated branch scope for user " + id);
        await transaction.CommitAsync();
        TempData["Success"] = "Branch access updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateAsync(string email, string password, string role, int? employeeId)
    {
        if (!new[]
        {
            "Admin",
            "HR",
            "Management",
            "Employee"
        }.Contains(role) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || (role != "Admin" && employeeId == null) || (employeeId != null && (!await db.Employees.AnyAsync(x => x.Id == employeeId) || await db.Users.AnyAsync(x => x.EmployeeId == employeeId))))
            return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync();
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
        await transaction.CommitAsync();
        TempData["Success"] = "Account created. The user must change their temporary password at first sign-in.";
        return RedirectToPage();
    }
}
