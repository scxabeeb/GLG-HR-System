using HrSystemDhg.Web.Services;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Hr;
[Authorize(Roles = "HR,Admin")]
public class DetailsModel(HrDbContext db) : PageModel
{
    public Employee? Employee;
    public List<Education> Education = [];
    public List<WorkExperience> Experience = [];
    public List<EmployeeAssignment> Assignments = [];
    public List<Branch> Branches = [];
    public List<Department> Departments = [];
    public List<SubDepartment> SubDepartments = [];
    public async Task<IActionResult> OnGetAsync(int employeeId)
    {
        Employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId);
        if (Employee == null)
            return NotFound();
        Education = await db.Educations.Where(x => x.EmployeeId == employeeId).ToListAsync();
        Experience = await db.WorkExperiences.Where(x => x.EmployeeId == employeeId).ToListAsync();
        Assignments = await db.EmployeeAssignments.Where(x => x.EmployeeId == employeeId).OrderByDescending(x => x.EffectiveFrom).ToListAsync();
        Branches = await db.Branchs.Where(x => x.IsActive).ToListAsync();
        Departments = await db.Departments.Where(x => x.IsActive).ToListAsync();
        SubDepartments = await db.SubDepartments.Where(x => x.IsActive).ToListAsync();
        return Page();
    }

    private async Task<IActionResult> Save(int employeeId, string action)
    {
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Employee {employeeId}: {action}" });
        await db.SaveChangesAsync();
        return RedirectToPage(new { employeeId });
    }

    public async Task<IActionResult> OnPostUpdateAsync(int employeeId, string fullName, string? phone, string? nationalId, string workEmail, [FromServices] Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> users)
    {
        var e = await db.Employees.SingleOrDefaultAsync(x => x.Id == employeeId);
        if (e == null)
            return NotFound();
        if (string.IsNullOrWhiteSpace(fullName) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(workEmail) || await db.Employees.IgnoreQueryFilters().AnyAsync(x => x.Id != employeeId && (x.WorkEmail == workEmail || nationalId != null && nationalId != "" && x.NationalId == nationalId)))
        {
            await OnGetAsync(employeeId);
            ModelState.AddModelError("", "Enter a valid name, unique work email and national ID.");
            return Page();
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        if (e.WorkEmail != workEmail)
        {
            var login = await users.Users.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.UserName == e.WorkEmail);
            if (login != null)
            {
                login.Email = workEmail;
                login.UserName = workEmail;
                var update = await users.UpdateAsync(login);
                if (!update.Succeeded)
                {
                    await OnGetAsync(employeeId);
                    foreach (var error in update.Errors)
                        ModelState.AddModelError("", error.Description);
                    return Page();
                }

                await users.UpdateSecurityStampAsync(login);
            }
        }

        e.FullName = fullName;
        e.WorkEmail = workEmail;
        e.Phone = phone ?? "";
        e.NationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId;
        var result = await Save(employeeId, "updated personal details");
        await tx.CommitAsync();
        return result;
    }

    public async Task<IActionResult> OnPostEducationAsync(int employeeId, string institution, string qualification, string? field)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId) || string.IsNullOrWhiteSpace(institution) || string.IsNullOrWhiteSpace(qualification))
            return BadRequest();
        db.Educations.Add(new() { EmployeeId = employeeId, Institution = institution, Qualification = qualification, FieldOfStudy = field ?? "" });
        return await Save(employeeId, "added education");
    }

    public async Task<IActionResult> OnPostExperienceAsync(int employeeId, string employer, string title, DateTime from, DateTime? to)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId) || string.IsNullOrWhiteSpace(employer) || string.IsNullOrWhiteSpace(title) || from == default || to < from)
            return BadRequest();
        db.WorkExperiences.Add(new() { EmployeeId = employeeId, Employer = employer, JobTitle = title, From = from, To = to });
        return await Save(employeeId, "added work experience");
    }

    public async Task<IActionResult> OnPostTransferAsync(int employeeId, int branchId, int departmentId, int? subDepartmentId, string title, DateTime effective)
    {
        var d = await db.Departments.SingleOrDefaultAsync(x => x.Id == departmentId && x.IsActive);
        if (d == null || !d.ServesBranch(branchId) || !await db.Branchs.AnyAsync(x => x.Id == branchId && x.IsActive) || !await db.Employees.AnyAsync(x => x.Id == employeeId) || effective == default || effective < (await db.Employees.SingleAsync(x => x.Id == employeeId)).HireDate || string.IsNullOrWhiteSpace(title) || (d.RequiresSubDepartment && subDepartmentId == null) || (subDepartmentId != null && !await db.SubDepartments.AnyAsync(x => x.Id == subDepartmentId && x.DepartmentId == departmentId && x.IsActive)))
            return BadRequest();
        var current = await db.EmployeeAssignments.Where(x => x.EmployeeId == employeeId).OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync();
        if (current != null)
        {
            if (effective <= current.EffectiveFrom)
                return BadRequest();
            current.EffectiveTo = effective.AddDays(-1);
        }

        db.EmployeeAssignments.Add(new() { EmployeeId = employeeId, BranchId = branchId, DepartmentId = departmentId, SubDepartmentId = subDepartmentId, JobTitle = title, EffectiveFrom = effective });
        return await Save(employeeId, "changed assignment");
    }

    private const string EducationColumns = "Institution,Qualification,Field";
    private const string ExperienceColumns = "Employer,Title,From,To";
    private const string TransferColumns = "BranchId,DepartmentId,SubDepartmentId,Title,Effective";

    public async Task<IActionResult> OnGetSampleAsync(int employeeId, string dataset)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId)) return NotFound();
        return dataset switch
        {
            "education" => RegistrationCsv.Download("education-sample", EducationColumns, [["Example college", "Diploma", "Administration"]]),
            "experience" => RegistrationCsv.Download("experience-sample", ExperienceColumns, [["Example employer", "Officer", BusinessClock.Today.AddYears(-2), BusinessClock.Today.AddYears(-1)]]),
            "assignments" => RegistrationCsv.Download("assignments-sample", TransferColumns, [[1, 1, null, "Officer", BusinessClock.Today]]),
            _ => NotFound()
        };
    }

    public async Task<IActionResult> OnGetExportAsync(int employeeId, string dataset)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        switch (dataset)
        {
            case "education":
                var education = await db.Educations.AsNoTracking().Where(x => x.EmployeeId == employeeId).OrderBy(x => x.Id).ToListAsync();
                return RegistrationCsv.Download("education", EducationColumns,
                    education.Select(x => new object?[] { x.Institution, x.Qualification, x.FieldOfStudy }));
            case "experience":
                var experience = await db.WorkExperiences.AsNoTracking().Where(x => x.EmployeeId == employeeId).OrderBy(x => x.From).ToListAsync();
                return RegistrationCsv.Download("experience", ExperienceColumns,
                    experience.Select(x => new object?[] { x.Employer, x.JobTitle, x.From, x.To }));
            case "assignments":
                var assignments = await db.EmployeeAssignments.AsNoTracking().Where(x => x.EmployeeId == employeeId).OrderBy(x => x.EffectiveFrom).ToListAsync();
                return RegistrationCsv.Download("assignments", TransferColumns,
                    assignments.Select(x => new object?[] { x.BranchId, x.DepartmentId, x.SubDepartmentId, x.JobTitle, x.EffectiveFrom }));
            default:
                return NotFound();
        }
    }

    public async Task<IActionResult> OnPostImportAsync(int employeeId, string dataset, IFormFile? upload)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId)) return NotFound();
        IActionResult Back() => RedirectToPage(new { employeeId });
        return dataset switch
        {
            "education" => await RegistrationCsv.ImportAsync(this, db, upload, EducationColumns, row =>
                OnPostEducationAsync(employeeId, row.Get<string>("Institution"), row.Get<string>("Qualification"), row.Optional("Field")), Back),
            "experience" => await RegistrationCsv.ImportAsync(this, db, upload, ExperienceColumns, row =>
                OnPostExperienceAsync(employeeId, row.Get<string>("Employer"), row.Get<string>("Title"), row.Get<DateTime>("From"), row.Get<DateTime?>("To")), Back),
            "assignments" => await RegistrationCsv.ImportAsync(this, db, upload, TransferColumns, row =>
                OnPostTransferAsync(employeeId, row.Get<int>("BranchId"), row.Get<int>("DepartmentId"), row.Get<int?>("SubDepartmentId"), row.Get<string>("Title"), row.Get<DateTime>("Effective")), Back),
            _ => NotFound()
        };
    }
}
