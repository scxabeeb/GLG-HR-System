using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class LeaveTypesModel(HrDbContext db) : PageModel
{
    [BindProperty]
    public LeaveType Input { get; set; } = new();

    public List<LeaveType> Types = [];
    public async Task OnGetAsync(int? id)
    {
        Types = await db.LeaveTypes.ToListAsync();
        if (id != null)
            Input = await db.LeaveTypes.SingleAsync(x => x.Id == id);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.Name) || Input.AnnualDays < 0 || Input.MinDays < 1 || Input.MaxDays < Input.MinDays)
        {
            Types = await db.LeaveTypes.ToListAsync();
            ModelState.AddModelError("", "Enter valid leave limits and name.");
            return Page();
        }

        if (Input.Id == 0)
            db.LeaveTypes.Add(Input);
        else
        {
            var t = await db.LeaveTypes.SingleAsync(x => x.Id == Input.Id);
            db.Entry(t).CurrentValues.SetValues(Input);
        }

        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = "Saved leave type " + Input.Name });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    private const string CsvColumns = "Name,AnnualDays,IsPaid,EnforceBalance,AccrueMonthly,RequiresDocument,MinDays,MaxDays,GenderRestriction,EmploymentRestriction";

    public IActionResult OnGetSample()
    {
        return RegistrationCsv.Download("leave-types-sample", CsvColumns, [["Example leave", 10, true, true, false, false, 1, 10, null, null]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var records = await db.LeaveTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("leave-types", CsvColumns, records.Select(x => new object?[] { x.Name, x.AnnualDays, x.IsPaid, x.EnforceBalance, x.AccrueMonthly, x.RequiresDocument, x.MinDays, x.MaxDays, x.GenderRestriction, x.EmploymentRestriction }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload)
    {
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            Input = new LeaveType();
            Input.Name = row.Get<string>("Name");
            Input.AnnualDays = row.Get<decimal>("AnnualDays");
            Input.IsPaid = row.Get<bool>("IsPaid");
            Input.EnforceBalance = row.Get<bool>("EnforceBalance");
            Input.AccrueMonthly = row.Get<bool>("AccrueMonthly");
            Input.RequiresDocument = row.Get<bool>("RequiresDocument");
            Input.MinDays = row.Get<int>("MinDays");
            Input.MaxDays = row.Get<int>("MaxDays");
            Input.GenderRestriction = row.Optional("GenderRestriction");
            Input.EmploymentRestriction = row.Optional("EmploymentRestriction");
            if (await db.LeaveTypes.AnyAsync(x => x.Name == Input.Name))
                throw new FormatException("A leave type with this name already exists.");
            return await OnPostAsync();
        });
    }
}
