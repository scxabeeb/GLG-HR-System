using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HrSystemDhg.Web.Pages.Hr;
[Authorize(Roles = "HR,Admin")]
public class RegisterModel(HrDbContext db) : PageModel
{
    [BindProperty]
    [Required, StringLength(50)]
    public string Number { get; set; } = "";

    [BindProperty]
    [Required, StringLength(200)]
    public string Name { get; set; } = "";

    [BindProperty]
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [BindProperty]
    public DateTime Hire { get; set; } = HrSystemDhg.Web.Services.BusinessClock.Today;

    [BindProperty]
    [Range(1, int.MaxValue, ErrorMessage = "Select a department.")]
    public int DepartmentId { get; set; }

    [BindProperty]
    [Required, StringLength(150)]
    public string Title { get; set; } = "";

    [BindProperty]
    public string? Pin { get; set; }

    [BindProperty]
    [Required, StringLength(128, MinimumLength = 10)]
    public string InitialPassword { get; set; } = "";

    [BindProperty]
    [Range(1, int.MaxValue, ErrorMessage = "Select a branch.")]
    public int BranchId { get; set; }

    [BindProperty]
    public int? SubDepartmentId { get; set; }

    public List<Branch> Branches = [];
    public List<SubDepartment> SubDepartments = [];
    public List<Department> Departments = [];
    public async Task OnGetAsync()
    {
        Branches = await db.Branchs.Where(x => x.IsActive).ToListAsync();
        Departments = await db.Departments.Where(x => x.IsActive).ToListAsync();
        SubDepartments = await db.SubDepartments.Where(x => x.IsActive).ToListAsync();
    }

    public async Task<IActionResult> OnGetDepartmentsAsync(int branchId) => new JsonResult(await db.Departments.Where(x => x.BranchId == branchId && x.IsActive).Select(x => new { x.Id, x.Name }).ToListAsync());
    public async Task<IActionResult> OnGetSubDepartmentsAsync(int departmentId) => new JsonResult(await db.SubDepartments.Where(x => x.DepartmentId == departmentId && x.IsActive).Select(x => new { x.Id, x.Name }).ToListAsync());
    public async Task<IActionResult> OnPostAsync([FromServices] Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> users)
    {
        await OnGetAsync();
        Number = Number?.Trim() ?? ""; Name = Name?.Trim() ?? ""; Email = Email?.Trim() ?? ""; Title = Title?.Trim() ?? "";
        Pin = string.IsNullOrWhiteSpace(Pin) ? null : Pin.Trim();
        if (Hire == default) ModelState.AddModelError(nameof(Hire), "Select a valid hire date.");
        if (Hire > HrSystemDhg.Web.Services.BusinessClock.Today && !(await db.Settings.FirstOrDefaultAsync() ?? new()).AllowFutureHires)
            ModelState.AddModelError(nameof(Hire), "Future hire dates are disabled in System settings.");
        if (await users.FindByEmailAsync(Email) != null || await users.FindByNameAsync(Email) != null)
            ModelState.AddModelError(nameof(Email), "An account already uses this email address.");
        if (!ModelState.IsValid) return Page();
        var dept = await db.Departments.SingleOrDefaultAsync(x => x.Id == DepartmentId && x.IsActive);
        if (dept == null || dept.BranchId != BranchId || (dept.RequiresSubDepartment && SubDepartmentId == null) || (SubDepartmentId != null && !await db.SubDepartments.AnyAsync(x => x.Id == SubDepartmentId && x.DepartmentId == DepartmentId && x.IsActive)) || !await db.Branchs.AnyAsync(x => x.Id == dept.BranchId && x.IsActive) || (Hire > HrSystemDhg.Web.Services.BusinessClock.Today && !(await db.Settings.FirstOrDefaultAsync() ?? new()).AllowFutureHires) || string.IsNullOrWhiteSpace(Number) || string.IsNullOrWhiteSpace(Name) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(Email))
        {
            ModelState.AddModelError("", "Valid active department, employee details and hire date are required.");
            return Page();
        }

        if (await db.Employees.IgnoreQueryFilters().AnyAsync(x => x.EmployeeNumber == Number || x.WorkEmail == Email || (Pin != null && x.ZkPin == Pin)))
        {
            ModelState.AddModelError("", "Employee number, email or PIN already exists.");
            return Page();
        }

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var e = new Employee
            {
                EmployeeNumber = Number,
                FullName = Name,
                RegistrationComplete = false,
                WorkEmail = Email,
                HireDate = Hire,
                ZkPin = string.IsNullOrWhiteSpace(Pin) ? null : Pin
            };
            db.Employees.Add(e);
            await db.SaveChangesAsync();
            db.EmployeeAssignments.Add(new() { EmployeeId = e.Id, BranchId = dept.BranchId, DepartmentId = dept.Id, SubDepartmentId = SubDepartmentId, JobTitle = Title, EffectiveFrom = Hire });
            var u = new ApplicationUser
            {
                UserName = Email,
                Email = Email,
                EmployeeId = e.Id,
                MustChangePassword = true
            };
            var result = await users.CreateAsync(u, InitialPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                await tx.RollbackAsync();
                db.ChangeTracker.Clear();
                return Page();
            }

            var roleResult = await users.AddToRoleAsync(u, "Employee");
            if (!roleResult.Succeeded)
                throw new InvalidOperationException("Seed roles before registering employees.");
            db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Registered employee {Number}" });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return RedirectToPage("Onboarding", new { employeeId = e.Id, step = 2 });
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", "Employee number, email, national ID or device PIN is already in use. Refresh and try again.");
            return Page();
        }
    }
}
