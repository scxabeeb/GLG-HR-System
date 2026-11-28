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

    [BindProperty] public List<int> AdditionalBranchIds { get; set; } = [];

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

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    public List<SubDepartment> Subs = [];
    public record UnitRow(int Id, string Name, string Code, string Parent, string Location, bool Active);
    public IEnumerable<UnitRow> UnitRows => (Kind switch
    {
        "Department" => Departments.Select(d => new UnitRow(d.Id,d.Name,d.Code,string.Join(", ", Branches.Where(b=>d.ServesBranch(b.Id)).Select(b=>b.Name)),"",d.IsActive)),
        "SubDepartment" => Subs.Select(s => new UnitRow(s.Id,s.Name,"",Departments.FirstOrDefault(d=>d.Id==s.DepartmentId)?.Name ?? "—","",s.IsActive)),
        _ => Branches.Select(b => new UnitRow(b.Id,b.Name,b.Code,"",string.Join(" · ",new[]{b.City,b.Address,b.Timezone}.Where(v=>!string.IsNullOrWhiteSpace(v))),b.IsActive))
    }).Where(r=>string.IsNullOrWhiteSpace(Search) || (r.Name+" "+r.Code+" "+r.Parent+" "+r.Location).Contains(Search,StringComparison.OrdinalIgnoreCase)).OrderBy(r=>r.Name);
    public List<Branch> Branches = [];
    public List<Department> Departments = [];
    private async Task Load()
    {
        Branches = await db.Branchs.OrderBy(b=>b.Name).ToListAsync();
        Departments = await db.Departments.OrderBy(d=>d.Name).ToListAsync();
        Subs = await db.SubDepartments.OrderBy(s=>s.Name).ToListAsync();
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (Kind is not ("Branch" or "Department" or "SubDepartment") || Id < 0) return BadRequest();
        await Load();
        if (Id > 0)
        {
            if (Kind == "Branch")
            {
                var b = await db.Branchs.SingleOrDefaultAsync(x => x.Id == Id);
                if (b == null) return NotFound();
                Name = b.Name;
                Code = b.Code;
                City = b.City;
                Address = b.Address;
                Timezone = b.Timezone;
                Active = b.IsActive;
            }
            else if (Kind == "Department")
            {
                var d = await db.Departments.SingleOrDefaultAsync(x => x.Id == Id);
                if (d == null) return NotFound();
                Name = d.Name;
                Code = d.Code;
                ParentId = d.BranchId;
                AdditionalBranchIds = d.AdditionalBranches.Select(b => b.BranchId).ToList();
                RequiresSub = d.RequiresSubDepartment;
                Active = d.IsActive;
            }
            else if (Kind == "SubDepartment")
            {
                var s = await db.SubDepartments.SingleOrDefaultAsync(x => x.Id == Id);
                if (s == null) return NotFound();
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
        if (!ModelState.IsValid) return Page();
        Name = (Name ?? "").Trim(); Code = (Code ?? "").Trim();
        City = (City ?? "").Trim(); Address = (Address ?? "").Trim();
        Timezone = (Timezone ?? "").Trim();
        if (Id < 0) return BadRequest();
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
                var branchIds = AdditionalBranchIds.Append(ParentId).Distinct().ToArray();
                if (string.IsNullOrWhiteSpace(Code) || branchIds.Any(id => !Branches.Any(b => b.Id == id)))
                    throw new InvalidOperationException("Select valid branches and a department code.");
                if (await db.Departments.AnyAsync(x => x.Id != Id && x.Code == Code &&
                    (branchIds.Contains(x.BranchId) || x.AdditionalBranches.Any(b => branchIds.Contains(b.BranchId)))))
                    throw new InvalidOperationException("This department code is already used in one of the selected branches.");
                var d = Id == 0 ? new Department() : await db.Departments.SingleAsync(x => x.Id == Id);
                if (Id > 0 && await db.EmployeeAssignments.AnyAsync(x => x.DepartmentId == Id && !branchIds.Contains(x.BranchId)))
                    throw new InvalidOperationException("A branch with employee assignment history cannot be removed. Transfer employees or retain that branch to preserve history.");
                var additional = branchIds.Where(id => id != ParentId).ToArray();
                d.AdditionalBranches.RemoveAll(b => !additional.Contains(b.BranchId));
                foreach (var branchId in additional.Where(id => !d.AdditionalBranches.Any(b => b.BranchId == id)))
                    d.AdditionalBranches.Add(new DepartmentBranch { BranchId = branchId });
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
                if (await db.SubDepartments.AnyAsync(x => x.Id != Id && x.DepartmentId == ParentId && x.Name == Name))
                    throw new InvalidOperationException("A sub-department with this name already exists in the selected department.");
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
            TempData["OrganizationNotice"] = $"{Kind} {Name} saved.";
            return RedirectToPage(null, null, new { Kind }, "organization-list");
        }
        catch (InvalidOperationException e)
        {
            ModelState.AddModelError("", e.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var organization = new OrganizationModel(db) { PageContext = PageContext, TempData = TempData };
        var result = await organization.OnPostDeleteAsync(Kind, Id);
        return result is RedirectToPageResult ? RedirectToPage(null, null, new { Kind }, "organization-list") : result;
    }

    private const string CsvColumns = "Kind,Name,Code,City,Address,Timezone,ParentId,Active,RequiresSub,AdditionalBranchIds";

    public IActionResult OnGetSample()
    {
        return RegistrationCsv.Download("organization-units-sample", CsvColumns, [["Branch", "Example office", "EXAMPLE", "Garowe", "", "Africa/Mogadishu", null, true, false, null]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var records = await db.Branchs.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        var departments = await db.Departments.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var units = await db.SubDepartments.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        return RegistrationCsv.Download("organization-units", CsvColumns,
            records.Select(x => new object?[] { "Branch", x.Name, x.Code, x.City, x.Address, x.Timezone, null, x.IsActive, false, null })
                .Concat(departments.Select(x => new object?[] { "Department", x.Name, x.Code, null, null, null, x.BranchId, x.IsActive, x.RequiresSubDepartment, string.Join(";", x.AdditionalBranches.Select(b=>b.BranchId)) }))
                .Concat(units.Select(x => new object?[] { "SubDepartment", x.Name, null, null, null, null, x.DepartmentId, x.IsActive, false, null })));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload)
    {
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            Id = 0;
            Kind = row.Get<string>("Kind");
            Name = row.Get<string>("Name");
            Code = row.Get<string>("Code");
            City = row.Get<string>("City");
            Address = row.Get<string>("Address");
            Timezone = row.Get<string>("Timezone");
            Active = row.Get<bool>("Active");
            RequiresSub = row.Get<bool>("RequiresSub");
            ParentId = row.Get<int?>("ParentId") ?? 0;
            AdditionalBranchIds = row.List("AdditionalBranchIds").Select(value => int.TryParse(value, out var id) && id > 0 ? id : throw new FormatException("Use semicolon-separated branch IDs.")).ToList();
            if (Kind == "Department" && string.IsNullOrWhiteSpace(Code))
                throw new FormatException("Enter a department code.");
            if (Kind == "SubDepartment" && await db.SubDepartments.AnyAsync(x => x.DepartmentId == ParentId && x.Name == Name))
                throw new FormatException("This sub-department already exists.");
            return await OnPostAsync();
        }, () => RedirectToPage(null, null, new { Kind }, "organization-list"));
    }
}
