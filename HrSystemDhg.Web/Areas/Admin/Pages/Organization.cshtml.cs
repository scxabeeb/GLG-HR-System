using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class OrganizationModel(HrDbContext db) : PageModel
{
    public List<Branch> Branches = [];
    public List<Department> Departments = [];
    public List<SubDepartment> Subs = [];
    public async Task OnGetAsync()
    {
        Branches = await db.Branchs.ToListAsync();
        Departments = await db.Departments.ToListAsync();
        Subs = await db.SubDepartments.ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string kind, int id)
    {
        bool used;
        object? unit;
        string name;
        switch (kind)
        {
            case "Branch":
                var branch = await db.Branchs.FindAsync(id); if (branch == null) return NotFound();
                unit = branch; name = branch.Name;
                used = await db.Departments.AnyAsync(x => x.BranchId == id || x.AdditionalBranches.Any(b => b.BranchId == id)) || await db.ZkDevices.AnyAsync(x => x.BranchId == id) ||
                    await db.EmployeeAssignments.AnyAsync(x => x.BranchId == id) || await db.UserBranchScopes.AnyAsync(x => x.BranchId == id) ||
                    await db.WorkRules.AnyAsync(x => x.BranchId == id) || await db.DeductionRules.AnyAsync(x => x.BranchId == id) ||
                    await db.PayrollPeriods.AnyAsync(x => x.BranchId == id) || await db.PayrollLines.AnyAsync(x => x.BranchId == id) ||
                    await db.Assets.AnyAsync(x => x.BranchId == id) || await db.AssetActivities.AnyAsync(x => x.BranchId == id) ||
                    await db.FleetVehicles.AnyAsync(x => x.BranchId == id) || await db.FleetTrips.AnyAsync(x => x.BranchId == id) ||
                    await db.FleetExpenses.AnyAsync(x => x.BranchId == id) || await db.Holidays.AnyAsync(x => x.BranchId == id) ||
                    await db.SavedReportViews.AnyAsync(x => x.BranchId == id);
                break;
            case "Department":
                var department = await db.Departments.FindAsync(id); if (department == null) return NotFound();
                unit = department; name = department.Name;
                used = await db.SubDepartments.AnyAsync(x => x.DepartmentId == id) || await db.EmployeeAssignments.AnyAsync(x => x.DepartmentId == id) ||
                    await db.ZkDevices.AnyAsync(x => x.DepartmentId == id) || await db.WorkRules.AnyAsync(x => x.DepartmentId == id) ||
                    await db.PayrollLines.AnyAsync(x => x.DepartmentId == id) || await db.Assets.AnyAsync(x => x.DepartmentId == id) ||
                    await db.SavedReportViews.AnyAsync(x => x.DepartmentId == id);
                break;
            case "SubDepartment":
                var sub = await db.SubDepartments.FindAsync(id); if (sub == null) return NotFound();
                unit = sub; name = sub.Name;
                used = await db.EmployeeAssignments.AnyAsync(x => x.SubDepartmentId == id) || await db.PayrollLines.AnyAsync(x => x.SubDepartmentId == id);
                break;
            default: return BadRequest();
        }
        if (used)
        {
            TempData["OrganizationNotice"] = $"{name} is linked to records or child units and cannot be deleted. Edit it and turn off Active to deactivate it.";
            return RedirectToPage();
        }
        db.Remove(unit);
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Deleted unused {kind} {id}: {name}" });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException { Number: 1451 })
        {
            db.ChangeTracker.Clear();
            TempData["OrganizationNotice"] = "This unit is linked to records and cannot be deleted. Deactivate it instead.";
            return RedirectToPage();
        }
        TempData["OrganizationNotice"] = $"{name} deleted.";
        return RedirectToPage();
    }
}
