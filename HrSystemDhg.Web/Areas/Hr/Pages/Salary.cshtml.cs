using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Hr;
[Authorize(Roles = "HR,Admin")]
public class SalaryModel(HrDbContext db) : PageModel
{
    public List<Employee> Employees = [];
    public List<SalaryVersion> Versions = [];
    [BindProperty(SupportsGet = true)]
    public int EmployeeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ReturnToWizard { get; set; }

    [BindProperty]
    public decimal Basic { get; set; }

    [BindProperty]
    public decimal Housing { get; set; }

    [BindProperty]
    public decimal Transport { get; set; }

    [BindProperty]
    public decimal Allowances { get; set; }

    [BindProperty]
    public string OtherName { get; set; } = "Other";

    [BindProperty]
    public string Currency { get; set; } = "USD";

    [BindProperty]
    public DateTime Effective { get; set; } = HrSystemDhg.Web.Services.BusinessClock.Today;

    [BindProperty]
    public string? Bank { get; set; }

    [BindProperty]
    public string? AccountName { get; set; }

    [BindProperty]
    public string? Account { get; set; }

    [BindProperty]
    public bool Draft { get; set; }

    [BindProperty]
    public bool Overtime { get; set; }

    public async Task OnGetAsync()
    {
        Employees = await db.Employees.ToListAsync();
        Versions = await db.SalaryVersions.OrderByDescending(x => x.EffectiveFrom).ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await OnGetAsync();
        if (Basic < 0 || Housing < 0 || Transport < 0 || Allowances < 0 || string.IsNullOrWhiteSpace(Currency) ||
            Currency.Length != 3 || !Currency.All(char.IsAsciiLetter) || Effective == default ||
            Effective.Year == 9999 || !Employees.Any(x => x.Id == EmployeeId))
            ModelState.AddModelError("", "Enter valid salary values, an effective date, and a three-letter currency code.");
        if (!ModelState.IsValid) return Page();

        var effectiveDate = Effective.Date;
        var nextDate = effectiveDate.AddDays(1);
        var sameDay = db.SalaryVersions.Where(x => x.EmployeeId == EmployeeId &&
            x.EffectiveFrom >= effectiveDate && x.EffectiveFrom < nextDate);
        if (await sameDay.AnyAsync())
        {
            ModelState.AddModelError(nameof(Effective), "This employee already has a salary version for that effective date. Use the existing version below.");
            return Page();
        }

        db.SalaryVersions.Add(new() { EmployeeId = EmployeeId, Basic = Basic, Housing = Housing, Transport = Transport, Allowances = Allowances, OtherAllowanceName = OtherName, Currency = Currency.ToUpperInvariant(), EffectiveFrom = effectiveDate, BankName = Bank, AccountName = AccountName ?? "", AccountNumber = Account, OvertimeEligible = Overtime, IsDraft = Draft });
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Added salary version for employee {EmployeeId}" });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException { Number: 1062 })
        {
            db.ChangeTracker.Clear();
            if (!await sameDay.AnyAsync()) throw;
            await OnGetAsync();
            ModelState.AddModelError(nameof(Effective), "This employee already has a salary version for that effective date. Use the existing version below.");
            return Page();
        }

        return ReturnToWizard ? RedirectToPage("Onboarding", new { employeeId = EmployeeId, step = 6 }) : RedirectToPage();
    }

    public async Task<IActionResult> OnPostPublishAsync(int id)
    {
        var s = await db.SalaryVersions.SingleAsync(x => x.Id == id);
        s.IsDraft = false;
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Published salary version {id}" });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    private const string CsvColumns = "EmployeeId,Basic,Housing,Transport,Allowances,OtherName,Currency,Effective,Bank,AccountName,Account,Draft,Overtime";

    public IActionResult OnGetSample()
    {
        return RegistrationCsv.Download("salary-versions-sample", CsvColumns, [[1, 1000, 100, 50, 0, "Other", "USD", BusinessClock.Today, null, null, null, true, false]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var records = await db.SalaryVersions.AsNoTracking().OrderBy(x => x.EmployeeId).ThenBy(x => x.EffectiveFrom).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("salary-versions", CsvColumns, records.Select(x => new object?[] { x.EmployeeId, x.Basic, x.Housing, x.Transport, x.Allowances, x.OtherAllowanceName, x.Currency, x.EffectiveFrom, x.BankName, x.AccountName, x.AccountNumber, x.IsDraft, x.OvertimeEligible }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload)
    {
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            EmployeeId = row.Get<int>("EmployeeId");
            Basic = row.Get<decimal>("Basic");
            Housing = row.Get<decimal>("Housing");
            Transport = row.Get<decimal>("Transport");
            Allowances = row.Get<decimal>("Allowances");
            OtherName = row.Get<string>("OtherName");
            Currency = row.Get<string>("Currency");
            Effective = row.Get<DateTime>("Effective");
            Bank = row.Optional("Bank");
            AccountName = row.Optional("AccountName");
            Account = row.Optional("Account");
            Draft = row.Get<bool>("Draft");
            Overtime = row.Get<bool>("Overtime");
            ReturnToWizard = false;
            return await OnPostAsync();
        });
    }
}
