using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class RulesModel(HrDbContext db) : PageModel
{
    [BindProperty]
    public WorkRule Rule { get; set; } = new()
    {
        EffectiveFrom = HrSystemDhg.Web.Services.BusinessClock.Today
    };

    [BindProperty]
    public decimal Absence { get; set; } = 1;

    [BindProperty]
    public decimal Late { get; set; } = .1m;

    public List<Branch> Branches = [];
    public List<Department> Departments = [];
    public List<Shift> Shifts = [];
    public List<WorkRule> Rules = [];
    public List<Holiday> Holidays = [];
    public async Task OnGetAsync()
    {
        Branches = await db.Branchs.ToListAsync();
        Departments = await db.Departments.ToListAsync();
        Shifts = await db.Shifts.Where(x => x.IsActive).ToListAsync();
        Rules = await db.WorkRules.OrderByDescending(x => x.EffectiveFrom).ToListAsync();
        Holidays = await db.Holidays.OrderBy(x => x.Date).ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await OnGetAsync();
        if (Rule.DailyHours <= 0 || Rule.DailyHours > 20 || Rule.StartHour < 0 || Rule.StartHour > 23 || Rule.StartMinute < 0 || Rule.StartMinute > 59 || Rule.BreakMinutes < 0 || Rule.BreakMinutes > 180 || Rule.GraceMinutes < 0 || Rule.OvertimeMultiplier < 0 || Absence < 0 || Late < 0 || Rule.EffectiveFrom == default || string.IsNullOrWhiteSpace(Rule.WorkingWeekdays) || Rule.WorkingWeekdays.Split(',').Any(x => !int.TryParse(x, out var n) || n < 0 || n > 6) || !new[]
        {
            "Flag",
            "Absent",
            "Half day"
        }.Contains(Rule.IncompletePolicy) || (Rule.DepartmentId != null && !Departments.Any(x => x.Id == Rule.DepartmentId && (Rule.BranchId == null || x.BranchId == Rule.BranchId))))
        {
            ModelState.AddModelError("", "Enter valid rule values and a consistent organization scope.");
            return Page();
        }

        Rule.WorkingWeekdays = string.Join(",", Rule.WorkingWeekdays.Split(',').Select(int.Parse).Distinct().OrderBy(x => x));
        Rule.Id = 0;
        db.WorkRules.Add(Rule);
        db.DeductionRules.Add(new() { EffectiveFrom = Rule.EffectiveFrom, BranchId = Rule.BranchId, AbsenceMultiplier = Absence, LatePerMinute = Late });
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = "Added effective work/deduction rules" });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostShiftAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest();
        db.Shifts.Add(new() { Name = name });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostHolidayAsync(string name, DateTime date, int? branchId)
    {
        if (string.IsNullOrWhiteSpace(name) || date == default || branchId != null && !await db.Branchs.AnyAsync(x => x.Id == branchId))
            return BadRequest();
        db.Holidays.Add(new() { Name = name, Date = date, BranchId = branchId });
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = "Added holiday " + name });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveHolidayAsync(int id)
    {
        var h = await db.Holidays.SingleAsync(x => x.Id == id);
        db.Holidays.Remove(h);
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = "Removed holiday " + id });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }
}
