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
}
