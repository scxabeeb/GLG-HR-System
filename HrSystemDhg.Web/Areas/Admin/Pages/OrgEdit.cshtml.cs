using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class OrgEditModel(HrDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Kind { get; set; } = "Branch";

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public string Name { get; set; } = "";

    [BindProperty]
    public string Code { get; set; } = "";

    [BindProperty]
    public string City { get; set; } = "";

    [BindProperty]
    public string Address { get; set; } = "";

    [BindProperty]
    public string Timezone { get; set; } = "Africa/Mogadishu";

    [BindProperty]
    public int ParentId { get; set; }

    [BindProperty]
    public bool Active { get; set; } = true;

    [BindProperty]
    public bool RequiresSub { get; set; }

    public List<Branch> Branches = [];
    public List<Department> Departments = [];
    private async Task Load()
    {
        Branches = await db.Branchs.ToListAsync();
        Departments = await db.Departments.ToListAsync();
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await Load();
        if (Id > 0)
        {
            if (Kind == "Branch")
            {
                var b = await db.Branchs.SingleAsync(x => x.Id == Id);
                Name = b.Name;
                Code = b.Code;
                City = b.City;
                Address = b.Address;
                Timezone = b.Timezone;
                Active = b.IsActive;
            }
            else if (Kind == "Department")
            {
                var d = await db.Departments.SingleAsync(x => x.Id == Id);
                Name = d.Name;
                Code = d.Code;
                ParentId = d.BranchId;
                RequiresSub = d.RequiresSubDepartment;
                Active = d.IsActive;
            }
            else if (Kind == "SubDepartment")
            {
                var s = await db.SubDepartments.SingleAsync(x => x.Id == Id);
                Name = s.Name;
                ParentId = s.DepartmentId;
                Active = s.IsActive;
            }
            else
                return BadRequest();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await Load();
        try
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("Name is required.");
            if (Kind == "Branch")
            {
                try
                {
                    TimeZoneInfo.FindSystemTimeZoneById(Timezone);
                }
                catch
                {
                    throw new InvalidOperationException("Enter a valid IANA timezone.");
                }

                if (string.IsNullOrWhiteSpace(Code) || await db.Branchs.AnyAsync(x => x.Id != Id && x.Code == Code))
                    throw new InvalidOperationException("A unique branch code is required.");
                var b = Id == 0 ? new Branch() : await db.Branchs.SingleAsync(x => x.Id == Id);
                b.Name = Name;
                b.Code = Code;
                b.City = City;
                b.Address = Address;
                b.Timezone = Timezone;
                b.IsActive = Active;
                if (Id == 0)
                    db.Branchs.Add(b);
            }
            else if (Kind == "Department")
            {
                if (!Branches.Any(x => x.Id == ParentId) || await db.Departments.AnyAsync(x => x.Id != Id && x.BranchId == ParentId && x.Code == Code))
                    throw new InvalidOperationException("Select a branch and unique department code.");
                var d = Id == 0 ? new Department() : await db.Departments.SingleAsync(x => x.Id == Id);
                if (Id > 0 && d.BranchId != ParentId && await db.EmployeeAssignments.AnyAsync(x => x.DepartmentId == Id))
                    throw new InvalidOperationException("Departments with assignment history cannot move branches. Create a new department and transfer employees.");
                d.Name = Name;
                d.Code = Code;
                d.BranchId = ParentId;
                d.RequiresSubDepartment = RequiresSub;
                d.IsActive = Active;
                if (Id == 0)
                    db.Departments.Add(d);
            }
            else if (Kind == "SubDepartment")
            {
                if (!Departments.Any(x => x.Id == ParentId))
                    throw new InvalidOperationException("Select a department.");
                var s = Id == 0 ? new SubDepartment() : await db.SubDepartments.SingleAsync(x => x.Id == Id);
                if (Id > 0 && s.DepartmentId != ParentId && await db.EmployeeAssignments.AnyAsync(x => x.SubDepartmentId == Id))
                    throw new InvalidOperationException("Sub-departments with history cannot move. Create a new unit.");
                s.Name = Name;
                s.DepartmentId = ParentId;
                s.IsActive = Active;
                if (Id == 0)
                    db.SubDepartments.Add(s);
            }
            else
                return BadRequest();
            db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Saved {Kind} {Name}; active={Active}" });
            await db.SaveChangesAsync();
            return RedirectToPage("Organization");
        }
        catch (InvalidOperationException e)
        {
            ModelState.AddModelError("", e.Message);
            return Page();
        }
    }
}
