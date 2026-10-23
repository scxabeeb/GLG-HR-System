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
        if (Basic < 0 || Housing < 0 || Transport < 0 || Allowances < 0 || string.IsNullOrWhiteSpace(Currency) || Currency.Length != 3 || !Currency.All(char.IsAsciiLetter) || Effective == default || !Employees.Any(x => x.Id == EmployeeId) || await db.SalaryVersions.AnyAsync(x => x.EmployeeId == EmployeeId && x.EffectiveFrom == Effective))
        {
            ModelState.AddModelError("", "Enter valid salary values, unique effective date and currency code.");
            return Page();
        }

        db.SalaryVersions.Add(new() { EmployeeId = EmployeeId, Basic = Basic, Housing = Housing, Transport = Transport, Allowances = Allowances, OtherAllowanceName = OtherName, Currency = Currency.ToUpperInvariant(), EffectiveFrom = Effective, BankName = Bank, AccountName = AccountName ?? "", AccountNumber = Account, OvertimeEligible = Overtime, IsDraft = Draft });
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Added salary version for employee {EmployeeId}" });
        await db.SaveChangesAsync();
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
}
