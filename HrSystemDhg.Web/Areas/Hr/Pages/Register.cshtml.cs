using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HrSystemDhg.Web.Pages.Hr;
[Authorize(Roles = "HR,Admin")]
public class RegisterModel(HrDbContext db, DocumentService? documents = null) : PageModel
{
    [BindProperty]
    [StringLength(50)]
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
    [StringLength(128)]
    public string InitialPassword { get; set; } = "";

    [BindProperty]
    [Range(1, int.MaxValue, ErrorMessage = "Select a branch.")]
    public int BranchId { get; set; }

    [BindProperty]
    public int? SubDepartmentId { get; set; }

    [BindProperty] public IFormFile? Photo { get; set; }

    [BindProperty] public int? TerminalId { get; set; }
    public List<ZkDevice> Terminals = [];

    public List<Branch> Branches = [];
    public List<SubDepartment> SubDepartments = [];
    public List<Department> Departments = [];
    public async Task OnGetAsync()
    {
        Terminals = await db.ZkDevices.Where(d => d.PushSerial != null && d.PushAddress != null).OrderBy(d => d.Name).ToListAsync();
        if (TerminalId == null && Terminals.Count == 1) TerminalId = Terminals[0].Id;
        Branches = await db.Branchs.Where(x => x.IsActive).ToListAsync();
        Departments = await db.Departments.Where(x => x.IsActive).ToListAsync();
        SubDepartments = await db.SubDepartments.Where(x => x.IsActive).ToListAsync();
    }

    public async Task<IActionResult> OnGetDepartmentsAsync(int branchId) => new JsonResult(await db.Departments.Where(x => x.BranchId == branchId && x.IsActive).Select(x => new { x.Id, x.Name }).ToListAsync());
    public async Task<IActionResult> OnGetSubDepartmentsAsync(int departmentId) => new JsonResult(await db.SubDepartments.Where(x => x.DepartmentId == departmentId && x.IsActive).Select(x => new { x.Id, x.Name }).ToListAsync());
    public async Task<IActionResult> OnPostAsync([FromServices] Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> users)
    {
        ModelState.Remove(nameof(Number));
        ModelState.Remove(nameof(InitialPassword));
        ModelState.Remove(nameof(Pin));
        await OnGetAsync();
        Number = Number?.Trim() ?? ""; Name = Name?.Trim() ?? ""; Email = Email?.Trim() ?? ""; Title = Title?.Trim() ?? "";
        Pin = null;
        var terminal = Terminals.SingleOrDefault(d => d.Id == TerminalId);
        if (TerminalId != null && terminal == null)
            ModelState.AddModelError(nameof(TerminalId), "Select a paired attendance terminal.");
        if (Hire == default) ModelState.AddModelError(nameof(Hire), "Select a valid hire date.");
        if (Hire > HrSystemDhg.Web.Services.BusinessClock.Today && !(await db.Settings.FirstOrDefaultAsync() ?? new()).AllowFutureHires)
            ModelState.AddModelError(nameof(Hire), "Future hire dates are disabled in System settings.");
        if (await users.FindByEmailAsync(Email) != null || await users.FindByNameAsync(Email) != null)
            ModelState.AddModelError(nameof(Email), "An account already uses this email address.");
        if (!ModelState.IsValid) return Page();
        var dept = await db.Departments.SingleOrDefaultAsync(x => x.Id == DepartmentId && x.IsActive);
        if (dept == null || dept.BranchId != BranchId || (dept.RequiresSubDepartment && SubDepartmentId == null) || (SubDepartmentId != null && !await db.SubDepartments.AnyAsync(x => x.Id == SubDepartmentId && x.DepartmentId == DepartmentId && x.IsActive)) || !await db.Branchs.AnyAsync(x => x.Id == dept.BranchId && x.IsActive) || (Hire > HrSystemDhg.Web.Services.BusinessClock.Today && !(await db.Settings.FirstOrDefaultAsync() ?? new()).AllowFutureHires) || string.IsNullOrWhiteSpace(Name) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(Email))
        {
            ModelState.AddModelError("", "Valid active department, employee details and hire date are required.");
            return Page();
        }

        if (await db.Employees.IgnoreQueryFilters().AnyAsync(x => x.WorkEmail == Email || (Pin != null && x.ZkPin == Pin)))
        {
            ModelState.AddModelError("", "Employee number, email or PIN already exists.");
            return Page();
        }

        if (Photo != null && (db.Database.CurrentTransaction != null || Path.GetExtension(Photo.FileName).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png")))
        {
            ModelState.AddModelError(nameof(Photo), "Choose a JPG or PNG photo on the registration form.");
            return Page();
        }

        int? photoId = null;
        string? photoPath = null;
        var committed = false;
        try
        {
            await using var tx = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync() : null;
            Number = await EmployeeNumbers.AllocateAsync(db);
            Pin = await EmployeeNumbers.AllocatePinAsync(db);
            InitialPassword = Number + "123";
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
                UserName = Number,
                Email = Email,
                EmployeeId = e.Id,
                MustChangePassword = true
            };
            // Only this generated first-login credential uses the requested EMPID123 format.
            // Password changes/resets continue through the normal Identity password policy.
            u.PasswordHash = users.PasswordHasher.HashPassword(u, InitialPassword);
            var result = await users.CreateAsync(u);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                if (tx != null) await tx.RollbackAsync();
                db.ChangeTracker.Clear();
                return Page();
            }

            var roleResult = await users.AddToRoleAsync(u, "Employee");
            if (!roleResult.Succeeded)
                throw new InvalidOperationException("Seed roles before registering employees.");
            if (terminal != null && Pin != null)
            {
                db.ZkDeviceCommands.Add(ZkEmployeeCommands.Create(terminal, e));
                db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Queued terminal sync for employee {e.Id} on device {terminal.Id}" });
            }
            db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Registered employee {Number}" });
            await db.SaveChangesAsync();
            if (tx != null)
            {
                if (Photo != null)
                {
                    var storage = documents ?? HttpContext.RequestServices.GetRequiredService<DocumentService>();
                    photoId = await storage.StoreAsync(e.Id, Photo, "Photo", null, User.Identity!.Name!);
                    var stored = await db.EmployeeDocuments.FindAsync(photoId.Value);
                    photoPath = Path.Combine(storage.Root, stored!.StoragePath);
                }
                await tx.CommitAsync();
                committed = true;
                if (TempData != null) TempData["EmployeeLoginCreated"] = e.Id;
            }
            if (terminal != null && Pin != null && TempData != null)
                TempData["TerminalRegistrationNotice"] = $"Employee saved and queued for {terminal.Name}. Check the employee directory for the terminal response.";
            return RedirectToPage("Onboarding", new { employeeId = e.Id, step = 2 });
        }
        catch (InvalidOperationException ex) when (Photo != null && !committed)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError(nameof(Photo), ex.Message);
            return Page();
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", "Employee number, email, national ID or device PIN is already in use. Refresh and try again.");
            return Page();
        }
        finally
        {
            if (!committed && photoPath != null) System.IO.File.Delete(photoPath);
        }
    }

    private const string CsvColumns = "Number,Name,Email,Hire,BranchId,DepartmentId,SubDepartmentId,Title,Pin,TerminalId,InitialPassword";

    public IActionResult OnGetSample()
    {
        return RegistrationCsv.Download("employees-sample", CsvColumns, [[null, "Example employee", "employee@example.com", BusinessClock.Today, 1, 1, null, "Officer", null, null, null]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var records = await db.Employees.AsNoTracking().OrderBy(e => e.EmployeeNumber)
            .Select(e => new { Employee = e, Assignment = db.EmployeeAssignments.Where(a => a.EmployeeId == e.Id)
                .OrderBy(a => a.EffectiveFrom).ThenBy(a => a.Id).FirstOrDefault() }).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("employees", CsvColumns, records.Select(x => new object?[] { x.Employee.EmployeeNumber, x.Employee.FullName, x.Employee.WorkEmail, x.Employee.HireDate, x.Assignment?.BranchId, x.Assignment?.DepartmentId, x.Assignment?.SubDepartmentId, x.Assignment?.JobTitle, x.Employee.ZkPin, null, null }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload, [FromServices] Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> users)
    {
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            ModelState.Clear();
            ModelState.Clear();
            Number = "";
            Name = row.Get<string>("Name");
            Email = row.Get<string>("Email");
            Hire = row.Get<DateTime>("Hire");
            BranchId = row.Get<int>("BranchId");
            DepartmentId = row.Get<int>("DepartmentId");
            SubDepartmentId = row.Get<int?>("SubDepartmentId");
            Title = row.Get<string>("Title");
            Pin = null;
            TerminalId = row.Get<int?>("TerminalId");
            InitialPassword = "";
            var validation = new List<ValidationResult>();
            Validator.TryValidateObject(this, new ValidationContext(this), validation, validateAllProperties: true);
            foreach (var error in validation)
                ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? "", error.ErrorMessage ?? "Invalid employee details.");
            return await OnPostAsync(users);
        });
    }
}
