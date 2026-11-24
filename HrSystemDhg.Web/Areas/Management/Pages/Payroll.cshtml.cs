using System.Globalization;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Management;
[Authorize(Roles = "Management,Admin,HR,Finance,General Secretary")]
public class PayrollModel(HrDbContext db, PayrollService payroll) : PageModel
{
    public List<PayrollPeriod> Periods = [];
    public List<PayrollLine> Lines = [];
    public List<PayrollDeduction> Deductions = [];
    public List<Branch> Branches = [];
    public MissingWorkRuleException? MissingRule { get; private set; }
    public int? RulePeriodId { get; private set; }
    public bool ApplyDeductions { get; private set; }
    [BindProperty(SupportsGet = true)] public string Month { get; set; } = BusinessClock.Today.ToString("yyyy-MM");
    [BindProperty(SupportsGet = true)] public int? PayrollBranchId { get; set; }
    public bool FirstGenerationOnly { get; private set; }
    [BindProperty(SupportsGet = true)] public int? PeriodId { get; set; }
    public PayrollPeriod? SelectedPeriod { get; private set; }
    public Dictionary<(int EmployeeId, DateTime Date), AttendanceDay> Attendance { get; private set; } = [];
    public List<SalaryRow> SalaryRows { get; private set; } = [];
    public record SalaryRow(int Id, string Number, string Name, bool Complete, SalaryVersion? Salary);
    public bool Scoped => db.Scoped;
    public bool CanGenerate => PayrollWorkflow.Generate(User);
    public bool CanReview => PayrollWorkflow.Review(User);
    public bool CanPublish => CanGenerate || CanReview;
    public bool CanReport => User.IsInRole("HR") || User.IsInRole("Admin") || User.IsInRole("Management");
    public bool CanDecide(string status, string action) => PayrollWorkflow.CanDecide(User, status, action);
    public List<AuditLog> WorkflowHistory = [];

    public Dictionary<int, string> Names = [];
    public async Task OnGetAsync()
    {
        Periods = await db.PayrollPeriods.OrderByDescending(x => x.From).ToListAsync();
        Branches = await db.Branchs.ToListAsync();
        Names = await db.Employees.ToDictionaryAsync(x => x.Id, x => x.FullName);
        if (!TryMonth(out var from, out var to)) return;
        if (db.Scoped && PayrollBranchId == null) PayrollBranchId = Branches.FirstOrDefault(b => b.IsActive)?.Id;
        SelectedPeriod = PeriodId.HasValue ? Periods.FirstOrDefault(p => p.Id == PeriodId) :
            Periods.Where(p => p.From <= to && p.To >= from && (p.BranchId == PayrollBranchId || p.BranchId == null))
                .OrderByDescending(p => p.BranchId == PayrollBranchId).ThenByDescending(p => p.From == from && p.To == to)
                .FirstOrDefault();
        if (SelectedPeriod is { } period)
        {
            WorkflowHistory = await db.AuditLogs.AsNoTracking().Where(a => (a.Action.StartsWith("Payroll " + period.Id + ":") || a.Action.StartsWith("Calculated payroll " + period.Id + " revision ")))
                .OrderByDescending(a => a.At).Take(50).ToListAsync();
            from = period.From;
            to = period.To;
            Lines = await db.PayrollLines.Where(l => l.PayrollPeriodId == period.Id && l.Revision == period.Revision).ToListAsync();
            var lineIds = Lines.Select(l => l.Id).ToArray();
            Deductions = await db.PayrollDeductions.Where(d => lineIds.Contains(d.PayrollLineId))
                .OrderBy(d => d.Date).ToListAsync();
            var employeeIds = Lines.Select(l => l.EmployeeId).ToArray();
            var days = await db.AttendanceDays.AsNoTracking()
                .Where(d => employeeIds.Contains(d.EmployeeId) && d.Date >= from && d.Date <= to).ToListAsync();
            Attendance = days.ToDictionary(d => (d.EmployeeId, d.Date.Date));
        }
        var scope = SelectedPeriod == null ? PayrollBranchId : SelectedPeriod.BranchId;
        var staff = db.Employees.AsNoTracking().Where(x => x.HireDate <= to &&
            (x.TerminationDate == null || x.TerminationDate >= from));
        if (scope.HasValue)
            staff = staff.Where(e => db.EmployeeAssignments.Any(a => a.EmployeeId == e.Id &&
                a.BranchId == scope && a.EffectiveFrom <= to && (a.EffectiveTo == null || a.EffectiveTo >= to)));
        if (SelectedPeriod != null && Lines.Count > 0)
        {
            var paidEmployees = Lines.Select(l => l.EmployeeId).ToArray();
            staff = db.Employees.AsNoTracking().Where(e => paidEmployees.Contains(e.Id));
        }
        SalaryRows = await staff.OrderBy(e => e.FullName).Select(e => new SalaryRow(e.Id, e.EmployeeNumber, e.FullName,
            e.RegistrationComplete, db.SalaryVersions.AsNoTracking().Where(s => s.EmployeeId == e.Id && !s.IsDraft &&
                s.EffectiveFrom <= to && (s.EffectiveTo == null || s.EffectiveTo >= to))
                .OrderByDescending(s => s.EffectiveFrom).FirstOrDefault())).ToListAsync();
    }

    private async Task<IActionResult> Attempt(Func<Task> work)
    {
        try
        {
            await work();
            return RedirectToPage(new { Month, PayrollBranchId, PeriodId });
        }
        catch (Exception e) when (e is InvalidOperationException or MySqlConnector.MySqlException { Number: 1205 or 1213 } or DbUpdateException { InnerException: MySqlConnector.MySqlException { Number: 1205 or 1213 } })
        {
            db.ChangeTracker.Clear();
            if (MissingRule == null) ModelState.AddModelError("", e is InvalidOperationException ? e.Message : "Another payroll request is in progress. Retry.");
            await OnGetAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCreateAsync(DateTime from, DateTime to, int days, int? branchId)
    {
        if (!CanGenerate) return Forbid();
        await using var transaction = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
        if (from == default || from > to || (to - from).TotalDays > 62 || days < 0 || days > 31 || (db.Scoped && branchId == null) || (branchId != null && !await db.Branchs.AnyAsync(x => x.Id == branchId && x.IsActive)) || await db.PayrollPeriods.IgnoreQueryFilters().AnyAsync(x => x.From <= to && x.To >= from && (x.BranchId == null || branchId == null || x.BranchId == branchId)))
        {
            await OnGetAsync();
            ModelState.AddModelError("", "Enter valid dates, working days and a non-overlapping branch period.");
            return Page();
        }

        db.PayrollPeriods.Add(new() { From = from, To = to, WorkingDays = days, BranchId = branchId });
        await db.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
        return RedirectToPage();
    }

    private bool TryMonth(out DateTime from, out DateTime to)
    {
        to = default;
        if (!DateTime.TryParseExact(Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out from)
            || from.Year is < 1900 or >= 9999)
            return false;
        to = from.AddMonths(1).AddDays(-1);
        return true;
    }

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        if (!CanGenerate) return Forbid();
        if (!ModelState.IsValid || !TryMonth(out var from, out var to) ||
            (db.Scoped && PayrollBranchId == null) || (PayrollBranchId != null &&
            !await db.Branchs.AnyAsync(b => b.Id == PayrollBranchId && b.IsActive)))
        {
            ModelState.AddModelError("", "Choose a month and an authorized branch.");
            await OnGetAsync();
            return Page();
        }

        int periodId;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var overlapping = await db.PayrollPeriods.IgnoreQueryFilters()
                .Where(p => p.From <= to && p.To >= from &&
                    (p.BranchId == null || PayrollBranchId == null || p.BranchId == PayrollBranchId))
                .OrderBy(p => p.Id).ToListAsync();
            var period = overlapping.FirstOrDefault(p => p.From == from && p.To == to && p.BranchId == PayrollBranchId);
            if (overlapping.Count != 0 && (period == null || overlapping.Count > 1))
            {
                var overlappingIds = overlapping.Select(p => p.Id).ToArray();
                var visible = await db.PayrollPeriods.AsNoTracking()
                    .Where(p => overlappingIds.Contains(p.Id))
                    .OrderByDescending(p => p.From == from && p.To == to && p.BranchId == PayrollBranchId)
                    .ThenByDescending(p => p.From).ThenByDescending(p => p.Id)
                    .Select(p => (int?)p.Id).FirstOrDefaultAsync();
                if (visible == null)
                    throw new InvalidOperationException("An existing payroll covers this month. Ask an administrator to open it.");
                TempData["PayrollNotice"] = "Opened an existing payroll that covers part or all of your selection.";
                return RedirectToPage(null, null, new { Month, PayrollBranchId, PeriodId = visible }, $"payroll-{visible}");
            }
            if (period == null)
            {
                // Zero selects the effective work calendar for each employee.
                period = new PayrollPeriod { From = from, To = to, BranchId = PayrollBranchId };
                db.PayrollPeriods.Add(period);
                await db.SaveChangesAsync();
            }
            periodId = period.Id;
            await transaction.CommitAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException ||
            (ex is MySqlConnector.MySqlException { Number: 1205 or 1213 }) ||
            (ex is DbUpdateException { InnerException: MySqlConnector.MySqlException { Number: 1205 or 1213 } }))
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", ex is InvalidOperationException ? ex.Message : "Another payroll request is in progress. Retry.");
            await OnGetAsync();
            return Page();
        }
        PeriodId = periodId;
        return await Calculate(periodId, true, true);
    }

    public Task<IActionResult> OnPostCalculateAsync(int id, bool apply, bool firstGenerationOnly = false) =>
        !CanGenerate ? Task.FromResult<IActionResult>(Forbid()) : Calculate(id, apply, firstGenerationOnly);

    private Task<IActionResult> Calculate(int id, bool apply, bool firstGenerationOnly) => Attempt(async () =>
    {
        if (!await db.PayrollPeriods.AnyAsync(p => p.Id == id))
            throw new InvalidOperationException("Choose a payroll period you can manage.");
        PeriodId = id;
        try
        {
            await payroll.CalculateAsync(id, apply, User.Identity!.Name!, firstGenerationOnly);
        }
        catch (MissingWorkRuleException ex)
        {
            MissingRule = ex;
            RulePeriodId = id;
            ApplyDeductions = apply;
            FirstGenerationOnly = firstGenerationOnly;
            throw;
        }
    });
    public Task<IActionResult> OnPostMarkPaidAsync(int id, bool confirmed) => !CanReview ? Task.FromResult<IActionResult>(Forbid()) : Attempt(async () =>
    {
        if (!confirmed || !await db.PayrollPeriods.AnyAsync(p => p.Id == id))
            throw new InvalidOperationException("Select a payroll you can manage and confirm payment has been made.");
        await payroll.DecideAsync(id, "Paid", User.Identity!.Name!, null, User);
    });

    public Task<IActionResult> OnPostTransitionAsync(int id, string action, string? reason) => action == "Paid"
        ? Task.FromResult<IActionResult>(BadRequest("Use Mark as paid and confirm payment."))
        : Attempt(() => payroll.DecideAsync(id, action, User.Identity!.Name!, reason, User));
    public Task<IActionResult> OnPostWaiveAttendanceAsync(int id, int? employeeId, string? reason, string? dateScope, DateTime[] dates) =>
        Waive(id, employeeId, reason, dateScope: dateScope, dates: dates);

    public Task<IActionResult> OnPostWaiveAbsencesAsync(int id, int? employeeId, string? reason) =>
        Waive(id, employeeId, reason, absencesOnly: true);

    private Task<IActionResult> Waive(int id, int? employeeId, string? reason, bool absencesOnly = false,
        string? dateScope = null, DateTime[]? dates = null) => Attempt(async () =>
    {
        if (!CanGenerate && !CanReview) throw new InvalidOperationException("Only HR or Admin/Finance can adjust attendance deductions.");
        PeriodId = id;
        if (!ModelState.IsValid || (!absencesOnly && employeeId is not >= 0))
            throw new InvalidOperationException("Choose a valid payroll and employee.");
        if (!absencesOnly && (dateScope is not ("all" or "selected") || (dateScope == "selected" && dates is not { Length: > 0 })))
            throw new InvalidOperationException("Choose all days or select at least one date.");
        var count = await payroll.WaiveAttendanceAsync(id, employeeId == 0 ? null : employeeId,
            reason, User.Identity!.Name!, absencesOnly, dateScope == "selected" ? dates : null);
        TempData["PayrollNotice"] = count == 0 ? "No applied attendance deductions match your selection." : $"Waived {count} attendance deduction(s).";
    });

    public Task<IActionResult> OnPostAdjustAsync(int deductionId, decimal amount, string reason) => !CanGenerate && !CanReview ? Task.FromResult<IActionResult>(Forbid()) : Attempt(() => payroll.AdjustAsync(deductionId, amount, reason, User.Identity!.Name!));
    public Task<IActionResult> OnPostPublishAsync(int id) => !CanPublish ? Task.FromResult<IActionResult>(Forbid()) : Attempt(() => payroll.PublishAsync(id, User.Identity!.Name!));
    public Task<IActionResult> OnPostOverrideAsync(int lineId, decimal? amount, string reason) => !CanGenerate && !CanReview ? Task.FromResult<IActionResult>(Forbid()) : Attempt(() => payroll.OverrideLineAsync(lineId, amount, reason, User.Identity!.Name!));

    private const string CsvColumns = "From,To,WorkingDays,BranchId";

    public IActionResult OnGetSample()
    {
        return RegistrationCsv.Download("payroll-periods-sample", CsvColumns, [[new DateTime(BusinessClock.Today.Year, BusinessClock.Today.Month, 1),
            new DateTime(BusinessClock.Today.Year, BusinessClock.Today.Month, 1).AddMonths(1).AddDays(-1), 0, 1]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var records = await db.PayrollPeriods.AsNoTracking().OrderBy(x => x.From).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("payroll-periods", CsvColumns, records.Select(x => new object?[] { x.From, x.To, x.WorkingDays, x.BranchId }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload)
    {
        if (!CanGenerate) return Task.FromResult<IActionResult>(Forbid());
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            return await OnPostCreateAsync(row.Get<DateTime>("From"), row.Get<DateTime>("To"),
                row.Get<int>("WorkingDays"), row.Get<int?>("BranchId"));
        });
    }
}
