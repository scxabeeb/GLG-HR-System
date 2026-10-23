using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Management;
[Authorize(Roles = "Management,Admin")]
public class PayrollModel(HrDbContext db, PayrollService payroll) : PageModel
{
    public List<PayrollPeriod> Periods = [];
    public List<PayrollLine> Lines = [];
    public List<PayrollDeduction> Deductions = [];
    public List<Branch> Branches = [];
    public bool Scoped => db.Scoped;

    public Dictionary<int, string> Names = [];
    public async Task OnGetAsync()
    {
        Periods = await db.PayrollPeriods.OrderByDescending(x => x.From).ToListAsync();
        Lines = await db.PayrollLines.ToListAsync();
        Deductions = await db.PayrollDeductions.ToListAsync();
        Branches = await db.Branchs.ToListAsync();
        Names = await db.Employees.ToDictionaryAsync(x => x.Id, x => x.FullName);
    }

    private async Task<IActionResult> Attempt(Func<Task> work)
    {
        try
        {
            await work();
            return RedirectToPage();
        }
        catch (InvalidOperationException e)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", e.Message);
            await OnGetAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCreateAsync(DateTime from, DateTime to, int days, int? branchId)
    {
        if (from == default || from > to || (to - from).TotalDays > 62 || days < 1 || days > 31 || (db.Scoped && branchId == null) || (branchId != null && !await db.Branchs.AnyAsync(x => x.Id == branchId && x.IsActive)) || await db.PayrollPeriods.AnyAsync(x => x.From <= to && x.To >= from && (x.BranchId == null || branchId == null || x.BranchId == branchId)))
        {
            await OnGetAsync();
            ModelState.AddModelError("", "Enter valid dates, working days and a non-overlapping branch period.");
            return Page();
        }

        db.PayrollPeriods.Add(new() { From = from, To = to, WorkingDays = days, BranchId = branchId });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public Task<IActionResult> OnPostCalculateAsync(int id, bool apply) => Attempt(() => payroll.CalculateAsync(id, apply, User.Identity!.Name!));
    public Task<IActionResult> OnPostTransitionAsync(int id, string action, string? reason) => Attempt(() => payroll.DecideAsync(id, action, User.Identity!.Name!, reason));
    public Task<IActionResult> OnPostAdjustAsync(int deductionId, decimal amount, string reason) => Attempt(() => payroll.AdjustAsync(deductionId, amount, reason, User.Identity!.Name!));
    public Task<IActionResult> OnPostPublishAsync(int id) => Attempt(() => payroll.PublishAsync(id, User.Identity!.Name!));
    public Task<IActionResult> OnPostOverrideAsync(int lineId, decimal? amount, string reason) => Attempt(() => payroll.OverrideLineAsync(lineId, amount, reason, User.Identity!.Name!));
}
