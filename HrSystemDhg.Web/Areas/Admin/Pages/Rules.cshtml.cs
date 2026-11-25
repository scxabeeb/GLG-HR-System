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
        EffectiveFrom = HrSystemDhg.Web.Services.BusinessClock.Today,
        UsePunchWindows = true, IncompletePolicy = "Absent", StartHour = 6, StartMinute = 30, DailyHours = 8, BreakMinutes = 0, GraceMinutes = 0, WorkingWeekdays = "0,1,2,3,4,6"
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
    public int? PayrollId { get; private set; }
    [BindProperty] public int[] WorkingDays { get; set; } = [0, 1, 2, 3, 4, 6];

    public async Task OnGetAsync(DateTime? effective = null, int? branchId = null, int? departmentId = null,
        int? shiftId = null, int? payrollId = null, int? templateId = null)
    {
        Branches = await db.Branchs.ToListAsync();
        Departments = await db.Departments.ToListAsync();
        Shifts = await db.Shifts.Where(x => x.IsActive).ToListAsync();
        Rules = await db.WorkRules.AsNoTracking().OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Id).ToListAsync();
        Holidays = await db.Holidays.OrderBy(x => x.Date).ToListAsync();
        PayrollId = payrollId;
        if (templateId.HasValue)
        {
            var source = Rules.FirstOrDefault(r => r.Id == templateId);
            if (source != null) { Rule = System.Text.Json.JsonSerializer.Deserialize<WorkRule>(System.Text.Json.JsonSerializer.Serialize(source))!; Rule.Id = 0; Rule.EffectiveFrom = BusinessClock.Today; WorkingDays = Rule.WorkingWeekdays.Split(',').Select(int.Parse).ToArray(); }
        }
        if (effective.HasValue)
        {
            Rule.EffectiveFrom = effective.Value.Date;
            Rule.BranchId = branchId;
            Rule.DepartmentId = departmentId;
            Rule.ShiftId = shiftId;
        }
    }

    public async Task<IActionResult> OnPostAsync(int? payrollId = null)
    {
        await OnGetAsync(payrollId: payrollId);
        if (Request.HasFormContentType && Request.Form.ContainsKey("WorkingDaysSubmitted"))
        {
            WorkingDays = Request.Form["WorkingDays"].Select(x => int.TryParse(x, out var day) ? day : -1).ToArray();
            Rule.WorkingWeekdays = string.Join(",", WorkingDays.Distinct().OrderBy(x => x));
        }
        if (Rule.BranchId != null && !Branches.Any(b => b.Id == Rule.BranchId)) ModelState.AddModelError("", "Select an existing branch.");
        if (Rule.ShiftId != null && !Shifts.Any(s => s.Id == Rule.ShiftId)) ModelState.AddModelError("", "Select an active shift.");
        if (!AttendanceMinutes.Valid(Rule)) ModelState.AddModelError("", "Check the time windows, required hours, formula and rate. Check-in must end before checkout begins; use a divisor of 1–366 days.");
        if (!ModelState.IsValid || Rule.DailyHours <= 0 || Rule.DailyHours > 20 || Rule.StartHour < 0 || Rule.StartHour > 23 || Rule.StartMinute < 0 || Rule.StartMinute > 59 || Rule.BreakMinutes < 0 || Rule.BreakMinutes > 180 || Rule.GraceMinutes < 0 || Rule.OvertimeMultiplier < 0 || Absence < 0 || Late < 0 || Rule.EffectiveFrom == default || string.IsNullOrWhiteSpace(Rule.WorkingWeekdays) || Rule.WorkingWeekdays.Split(',').Any(x => !int.TryParse(x, out var n) || n < 0 || n > 6) || !new[]
        {
            "Flag",
            "Absent",
            "Half day"
        }.Contains(Rule.IncompletePolicy) || (Rule.DepartmentId != null && !Departments.Any(x => x.Id == Rule.DepartmentId && (Rule.BranchId == null || x.BranchId == Rule.BranchId))))
        {
            ModelState.AddModelError("", "Enter valid rule values and a consistent organization scope.");
            return Page();
        }

        if (Rule.UsePunchWindows) { Rule.StartHour = Rule.CheckInFrom.Hours; Rule.StartMinute = Rule.CheckInFrom.Minutes; }
        Rule.WorkingWeekdays = string.Join(",", Rule.WorkingWeekdays.Split(',').Select(int.Parse).Distinct().OrderBy(x => x));
        if (AttendanceMinutes.CreditsFullDay(Rule)) Rule.IncompletePolicy = "Absent";
        if (Rule.UsePunchWindows && Rule.CreditFullDayForValidPunches) Rule.IncompletePolicy = "Absent";
        Rule.Id = 0;
        db.WorkRules.Add(Rule);
        db.DeductionRules.Add(new() { EffectiveFrom = Rule.EffectiveFrom, BranchId = Rule.BranchId, AbsenceMultiplier = Absence, LatePerMinute = Late });
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action =  $"Added attendance rule from {Rule.EffectiveFrom:yyyy-MM-dd}; {Rule.DailyHours} hours, days {Rule.WorkingWeekdays}; {(AttendanceMinutes.CreditsFullDay(Rule) ? "full-day credit for valid window pair; missing pair absent" : "actual-duration attendance")}" });
        await db.SaveChangesAsync();
        if (payrollId.HasValue)
            return RedirectToPage("/Payroll", null, new { area = "Management", PeriodId = payrollId }, $"payroll-{payrollId}");
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

    private const string CsvColumns = "EffectiveFrom,BranchId,DepartmentId,ShiftId,DailyHours,StartHour,StartMinute,BreakMinutes,GraceMinutes,WorkingWeekdays,IncompletePolicy,OvertimeMultiplier,WeekendOvertime,Absence,Late,UsePunchWindows,CheckInFrom,CheckInUntil,CheckOutFrom,CheckOutUntil,MinuteRateMode,SalaryDivisor,FixedMinuteRate,CreditFullDayForValidPunches";

    public IActionResult OnGetSample(string? dataset)
    {
        if (dataset == "shifts") return RegistrationCsv.Download("shifts-sample", "Name", [["Morning shift"]]);
        if (dataset == "holidays") return RegistrationCsv.Download("holidays-sample", "Name,Date,BranchId", [["Example holiday", BusinessClock.Today.AddDays(1), null]]);
        return RegistrationCsv.Download("work-rules-sample", CsvColumns, [[BusinessClock.Today, null, null, null, 8, 6, 30, 0, 0, "0,1,2,3,4,6", "Absent", 1.5m, false, 1, 0, true, "06:30", "10:30", "12:30", "15:30", "Salary", 26, 0, true]]);
    }

    public async Task<IActionResult> OnGetExportAsync(string? dataset)
    {
        if (dataset == "shifts")
        {
            var shifts = await db.Shifts.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
            return RegistrationCsv.Download("shifts", "Name", shifts.Where(x => x.IsActive).Select(x => new object?[] { x.Name }));
        }
        if (dataset == "holidays")
        {
            var holidays = await db.Holidays.AsNoTracking().OrderBy(x => x.Date).ToListAsync();
            return RegistrationCsv.Download("holidays", "Name,Date,BranchId", holidays.Select(x => new object?[] { x.Name, x.Date, x.BranchId }));
        }
        var records = await db.WorkRules.AsNoTracking().OrderBy(x => x.EffectiveFrom).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        var deductions = (await db.DeductionRules.AsNoTracking().OrderByDescending(x => x.Id).ToListAsync())
            .ToLookup(x => (x.BranchId, x.EffectiveFrom));
        return RegistrationCsv.Download("work-rules", CsvColumns, records.Select(x => new object?[] { x.EffectiveFrom, x.BranchId, x.DepartmentId, x.ShiftId, x.DailyHours, x.StartHour, x.StartMinute, x.BreakMinutes, x.GraceMinutes, x.WorkingWeekdays, x.IncompletePolicy, x.OvertimeMultiplier, x.WeekendOvertime, deductions[(x.BranchId, x.EffectiveFrom)].FirstOrDefault()?.AbsenceMultiplier, deductions[(x.BranchId, x.EffectiveFrom)].FirstOrDefault()?.LatePerMinute, x.UsePunchWindows, x.CheckInFrom, x.CheckInUntil, x.CheckOutFrom, x.CheckOutUntil, x.MinuteRateMode, x.SalaryDivisor, x.FixedMinuteRate, x.CreditFullDayForValidPunches }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload, string? dataset)
    {
        if (dataset == "shifts")
            return RegistrationCsv.ImportAsync(this, db, upload, "Name", async row =>
            {
                var name = row.Get<string>("Name");
                if (await db.Shifts.AnyAsync(x => x.Name == name))
                    throw new FormatException("This shift already exists.");
                return await OnPostShiftAsync(name);
            });
        if (dataset == "holidays")
            return RegistrationCsv.ImportAsync(this, db, upload, "Name,Date,BranchId", async row =>
            {
                var name = row.Get<string>("Name");
                var date = row.Get<DateTime>("Date").Date;
                var branch = row.Get<int?>("BranchId");
                if (await db.Holidays.AnyAsync(x => x.Name == name && x.Date == date && x.BranchId == branch))
                    throw new FormatException("This holiday already exists.");
                return await OnPostHolidayAsync(name, date, branch);
            });
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            Rule = new WorkRule { UsePunchWindows = row.Get<bool>("UsePunchWindows") };
            Rule.CheckInFrom = row.Get<TimeSpan>("CheckInFrom");
            Rule.CheckInUntil = row.Get<TimeSpan>("CheckInUntil");
            Rule.CheckOutFrom = row.Get<TimeSpan>("CheckOutFrom");
            Rule.CheckOutUntil = row.Get<TimeSpan>("CheckOutUntil");
            Rule.MinuteRateMode = row.Get<string>("MinuteRateMode");
            Rule.SalaryDivisor = row.Get<decimal>("SalaryDivisor");
            Rule.FixedMinuteRate = row.Get<decimal>("FixedMinuteRate");
            Rule.CreditFullDayForValidPunches = row.Get<bool>("CreditFullDayForValidPunches");
            Rule.EffectiveFrom = row.Get<DateTime>("EffectiveFrom");
            Rule.BranchId = row.Get<int?>("BranchId");
            Rule.DepartmentId = row.Get<int?>("DepartmentId");
            Rule.ShiftId = row.Get<int?>("ShiftId");
            Rule.DailyHours = row.Get<decimal>("DailyHours");
            Rule.StartHour = row.Get<int>("StartHour");
            Rule.StartMinute = row.Get<int>("StartMinute");
            Rule.BreakMinutes = row.Get<int>("BreakMinutes");
            Rule.GraceMinutes = row.Get<int>("GraceMinutes");
            Rule.WorkingWeekdays = row.Get<string>("WorkingWeekdays");
            Rule.IncompletePolicy = row.Get<string>("IncompletePolicy");
            Rule.OvertimeMultiplier = row.Get<decimal>("OvertimeMultiplier");
            Rule.WeekendOvertime = row.Get<bool>("WeekendOvertime");
            Absence = row.Get<decimal>("Absence");
            Late = row.Get<decimal>("Late");
            if (Rule.BranchId != null && !await db.Branchs.AnyAsync(x => x.Id == Rule.BranchId))
                throw new FormatException("Select an existing branch.");
            if (Rule.ShiftId != null && !await db.Shifts.AnyAsync(x => x.Id == Rule.ShiftId && x.IsActive))
                throw new FormatException("Select an active shift.");
            if (await db.WorkRules.AnyAsync(x => x.EffectiveFrom == Rule.EffectiveFrom && x.BranchId == Rule.BranchId &&
                x.DepartmentId == Rule.DepartmentId && x.ShiftId == Rule.ShiftId))
                throw new FormatException("A rule already exists for that scope and date.");
            return await OnPostAsync();
        });
    }
}
