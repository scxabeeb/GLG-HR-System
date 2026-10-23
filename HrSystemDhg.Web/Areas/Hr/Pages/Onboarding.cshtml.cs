using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Hr;
[Authorize(Roles = "HR,Admin")]
public class OnboardingModel(HrDbContext db, CalendarService calendar, DocumentService documents) : PageModel
{
    public string[] Labels = ["Assignment", "Personal", "Education", "Experience", "Salary", "Documents"];
    [BindProperty(SupportsGet = true)]
    public int EmployeeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Step { get; set; } = 2;

    public Employee Employee = null !;
    public List<Employee> Managers = [];
    public List<Education> Education = [];
    public List<WorkExperience> Experience = [];
    public List<EmployeeDocument> Documents = [];
    public int SalaryCount;
    [BindProperty]
    public string Gender { get; set; } = "";

    [BindProperty]
    public DateTime? Birth { get; set; }

    [BindProperty]
    public string Nationality { get; set; } = "";

    [BindProperty]
    public string? NationalId { get; set; }

    [BindProperty]
    public string Phone { get; set; } = "";

    [BindProperty]
    public string PersonalEmail { get; set; } = "";

    [BindProperty]
    public string Address { get; set; } = "";

    [BindProperty]
    public string Marital { get; set; } = "";

    [BindProperty]
    public int Dependents { get; set; }

    [BindProperty]
    public string Employment { get; set; } = "Permanent";

    [BindProperty]
    public int? ManagerId { get; set; }

    [BindProperty]
    public string Emergency { get; set; } = "";

    [BindProperty]
    public string Relation { get; set; } = "";

    [BindProperty]
    public string EmergencyPhone { get; set; } = "";

    [BindProperty]
    public string? Institution { get; set; }

    [BindProperty]
    public string? Qualification { get; set; }

    [BindProperty]
    public string? Field { get; set; }

    [BindProperty]
    public int? StartYear { get; set; }

    [BindProperty]
    public int? EndYear { get; set; }

    [BindProperty]
    public string? Grade { get; set; }

    [BindProperty]
    public string? Employer { get; set; }

    [BindProperty]
    public string? JobTitle { get; set; }

    [BindProperty]
    public string? Country { get; set; }

    [BindProperty]
    public DateTime? WorkFrom { get; set; }

    [BindProperty]
    public DateTime? WorkTo { get; set; }

    [BindProperty]
    public string? Reference { get; set; }

    [BindProperty]
    public string? Responsibilities { get; set; }

    [BindProperty]
    public IFormFile? Upload { get; set; }

    [BindProperty]
    public string DocumentType { get; set; } = "Contract";

    [BindProperty]
    public DateTime? Expiry { get; set; }

    private async Task Load()
    {
        Employee = await db.Employees.SingleAsync(x => x.Id == EmployeeId);
        Managers = await db.Employees.Where(x => x.Id != EmployeeId).ToListAsync();
        Education = await db.Educations.Where(x => x.EmployeeId == EmployeeId).ToListAsync();
        Experience = await db.WorkExperiences.Where(x => x.EmployeeId == EmployeeId).ToListAsync();
        Documents = await db.EmployeeDocuments.Where(x => x.EmployeeId == EmployeeId).ToListAsync();
        SalaryCount = await db.SalaryVersions.CountAsync(x => x.EmployeeId == EmployeeId && !x.IsDraft);
        Step = Math.Clamp(Step, 1, 6);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await db.Employees.AnyAsync(x => x.Id == EmployeeId))
            return NotFound();
        await Load();
        Gender = Employee.Gender;
        Birth = Employee.DateOfBirth;
        Nationality = Employee.Nationality;
        NationalId = Employee.NationalId;
        Phone = Employee.Phone;
        PersonalEmail = Employee.PersonalEmail;
        Address = Employee.Address;
        Marital = Employee.MaritalStatus;
        Dependents = Employee.Dependents;
        Employment = Employee.EmploymentType;
        ManagerId = Employee.ManagerId;
        Emergency = Employee.EmergencyContact;
        Relation = Employee.EmergencyRelation;
        EmergencyPhone = Employee.EmergencyPhone;
        return Page();
    }

    public async Task<IActionResult> OnPostSkipAsync()
    {
        if (!await db.Employees.AnyAsync(x => x.Id == EmployeeId)) return NotFound();
        if (Step < 2 || Step > 5) return BadRequest();
        return RedirectToPage(new { employeeId = EmployeeId, step = Step + 1 });
    }

    public async Task<IActionResult> OnPostAsync(bool repeat)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == EmployeeId))
            return NotFound();
        await Load();
        if (!ModelState.IsValid) return Page();
        try
        {
            if (Step == 2)
            {
                if (!RegistrationChoices.ValidOptionalChoice(Nationality, RegistrationChoices.Nationalities)) ModelState.AddModelError(nameof(Nationality), "Select a nationality from the list.");
                if (!RegistrationChoices.ValidOptionalChoice(Marital, RegistrationChoices.MaritalStatuses)) ModelState.AddModelError(nameof(Marital), "Select a marital status from the list.");
                if (!RegistrationChoices.ValidOptionalChoice(Gender, new[] { "Female", "Male" })) ModelState.AddModelError(nameof(Gender), "Select a gender from the list.");
                if (!new[] { "Permanent", "Contract", "Intern" }.Contains(Employment)) ModelState.AddModelError(nameof(Employment), "Select an employment type.");
                if (!string.IsNullOrWhiteSpace(PersonalEmail) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(PersonalEmail)) ModelState.AddModelError(nameof(PersonalEmail), "Enter a valid personal email.");
                if (Birth != null && (Birth.Value.Year < 1900 || Birth >= Employee.HireDate)) ModelState.AddModelError(nameof(Birth), "Birth date must be before the hire date and after 1899.");
                if (!ModelState.IsValid) return Page();
                if (Birth > HrSystemDhg.Web.Services.BusinessClock.Today || Dependents < 0 || !string.IsNullOrWhiteSpace(NationalId) && await db.Employees.IgnoreQueryFilters().AnyAsync(x => x.Id != EmployeeId && x.NationalId == NationalId))
                    throw new InvalidOperationException("Invalid date, dependents or duplicate national ID.");
                if (ManagerId != null)
                {
                    var org = await calendar.AssignmentAsync(EmployeeId, HrSystemDhg.Web.Services.BusinessClock.Today < Employee.HireDate ? Employee.HireDate : HrSystemDhg.Web.Services.BusinessClock.Today);
                    var manager = await calendar.AssignmentAsync(ManagerId.Value, HrSystemDhg.Web.Services.BusinessClock.Today);
                    if (ManagerId == EmployeeId || manager.BranchId != org.BranchId)
                        throw new InvalidOperationException("Select a manager in the same branch.");
                }

                Employee.Gender = Gender?.Trim() ?? "";
                Employee.DateOfBirth = Birth;
                Employee.Nationality = Nationality?.Trim() ?? "";
                Employee.NationalId = string.IsNullOrWhiteSpace(NationalId) ? null : NationalId;
                Employee.Phone = Phone?.Trim() ?? "";
                Employee.PersonalEmail = PersonalEmail?.Trim() ?? "";
                Employee.Address = Address?.Trim() ?? "";
                Employee.MaritalStatus = Marital?.Trim() ?? "";
                Employee.Dependents = Dependents;
                Employee.EmploymentType = Employment;
                Employee.ManagerId = ManagerId;
                Employee.EmergencyContact = Emergency?.Trim() ?? "";
                Employee.EmergencyRelation = Relation?.Trim() ?? "";
                Employee.EmergencyPhone = EmergencyPhone?.Trim() ?? "";
            }

            if (Step == 3 && (!string.IsNullOrWhiteSpace(Institution) || !string.IsNullOrWhiteSpace(Qualification) || !string.IsNullOrWhiteSpace(Field) || StartYear != null || EndYear != null || !string.IsNullOrWhiteSpace(Grade)))
            {
                if (string.IsNullOrWhiteSpace(Institution) || string.IsNullOrWhiteSpace(Qualification) || EndYear < StartYear || StartYear < 1900 || EndYear < 1900 || StartYear > BusinessClock.Today.Year || EndYear > BusinessClock.Today.Year)
                    throw new InvalidOperationException("Enter a qualification and valid years.");
                db.Educations.Add(new() { EmployeeId = EmployeeId, Institution = Institution, Qualification = Qualification, FieldOfStudy = Field ?? "", StartYear = StartYear, EndYear = EndYear, Grade = Grade ?? "" });
            }

            if (Step == 4 && (!string.IsNullOrWhiteSpace(Employer) || !string.IsNullOrWhiteSpace(JobTitle) || WorkFrom != null || WorkTo != null || !string.IsNullOrWhiteSpace(Country) || !string.IsNullOrWhiteSpace(Responsibilities) || !string.IsNullOrWhiteSpace(Reference)))
            {
                if (string.IsNullOrWhiteSpace(Employer) || WorkFrom == null || WorkTo < WorkFrom || WorkFrom > BusinessClock.Today || WorkTo > BusinessClock.Today || string.IsNullOrWhiteSpace(JobTitle))
                    throw new InvalidOperationException("Enter a job title and valid work dates.");
                db.WorkExperiences.Add(new() { EmployeeId = EmployeeId, Employer = Employer, JobTitle = JobTitle, Country = Country ?? "", From = WorkFrom.Value, To = WorkTo, Responsibilities = Responsibilities ?? "", Reference = Reference ?? "" });
            }

            
            if (Step == 6)
            {
                if (Upload != null)
                    await documents.StoreAsync(EmployeeId, Upload, DocumentType, Expiry, User.Identity!.Name!);
                if (!repeat)
                {
                    if (!await db.EmployeeAssignments.AnyAsync(x => x.EmployeeId == EmployeeId))
                        throw new InvalidOperationException("A valid assignment is required.");
                    if(!Employee.RegistrationComplete)
                    {
                        var assignment=await db.EmployeeAssignments.Where(x=>x.EmployeeId==EmployeeId).OrderByDescending(x=>x.EffectiveFrom).FirstAsync();
                        var department=await db.Departments.SingleAsync(x=>x.Id==assignment.DepartmentId);
                        if(!department.IsActive||department.BranchId!=assignment.BranchId||!await db.Branchs.AnyAsync(x=>x.Id==assignment.BranchId&&x.IsActive)||(department.RequiresSubDepartment&&assignment.SubDepartmentId==null)||(assignment.SubDepartmentId!=null&&!await db.SubDepartments.AnyAsync(x=>x.Id==assignment.SubDepartmentId&&x.DepartmentId==department.Id&&x.IsActive)))
                            throw new InvalidOperationException("Complete registration under an active, valid organization assignment.");
                    }
                    Employee.RegistrationComplete = SalaryCount > 0;
                }
            }

            db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Employee {EmployeeId}: onboarding step {Step}" });
            await db.SaveChangesAsync();
            if (Step == 6 && !repeat)
                {
                TempData["Success"] = SalaryCount > 0 ? "Employee registration completed." : "Employee saved. Add a published salary and finish onboarding later to enable payroll.";
                return RedirectToPage("Details", new { employeeId = EmployeeId });
            }
            return RedirectToPage(new { employeeId = EmployeeId, step = repeat ? Step : Math.Min(6, Step + 1) });
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", "Unable to save these details. The national ID may already be in use. Your employee record is saved; correct the details and retry.");
            return Page();
        }
        catch (InvalidOperationException e)
        {
            ModelState.AddModelError("", e.Message);
            return Page();
        }
    }
}
