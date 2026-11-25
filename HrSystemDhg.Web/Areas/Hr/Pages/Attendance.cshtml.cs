using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Hr;
[Authorize(Roles = "HR,Management,Admin")]
public class AttendanceModel(HrDbContext db, AttendanceService attendance, CalendarService calendar) : PageModel
{
    public sealed record PunchRow(DateTime Time, string Pin, string? Employee, string? Device);
    public List<PunchRow> TodayPunches = [];
    public List<ZkPunchReview> ClockReviews = [];
    [BindProperty(SupportsGet = true)] public bool ClockHistory { get; set; }
    private IQueryable<ZkPunchReview> VisibleClockReviews() => db.ZkPunchReviews.Where(r => !db.Scoped || db.Employees.Any(e => e.ZkPin == r.Pin));
    public static string HoursAndMinutes(decimal hours)
    {
        var minutes = (int)Math.Round(Math.Max(0,hours) * 60m);
        return $"{minutes / 60}h {minutes % 60:00}m";
    }
    [BindProperty(SupportsGet = true)] public int? EmployeeFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string RowsPerPage { get; set; }="20";
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; }=1;
    public ListPager Paging { get; private set; }=new("20",1,0);
    public string PageLink(int number)=>(Url.Page("./Attendance",new{From=From.ToString("yyyy-MM-dd"),To=To.ToString("yyyy-MM-dd"),EmployeeFilter,RowsPerPage,PageNumber=number})??"")+"#attendance-daily";
    public List<AttendanceDay> Items = [];
    public List<Employee> Employees = [];
    public List<ZkDevice> Devices = [];
    public List<string> Unmapped = [];
    public Dictionary<int, string> Names = [];
    [BindProperty(SupportsGet = true)]
    public DateTime From { get; set; } = HrSystemDhg.Web.Services.BusinessClock.Today.AddDays(-30);

    [BindProperty(SupportsGet = true)]
    public DateTime To { get; set; } = HrSystemDhg.Web.Services.BusinessClock.Today;

    public async Task OnGetAsync()
    {
        Employees = await db.Employees.OrderBy(e=>e.FullName).ThenBy(e=>e.EmployeeNumber).ToListAsync();
        if (EmployeeFilter.HasValue && !Employees.Any(e=>e.Id==EmployeeFilter))
            ModelState.AddModelError("", "Select an employee within your authorized scope.");
        if (From.Date>To.Date) ModelState.AddModelError("", "The start date must be on or before the end date.");
        TodayPunches = await LoadTodayPunchesAsync();
        ClockReviews = await VisibleClockReviews().AsNoTracking().Where(r => ClockHistory || r.ReviewedUtc == null).OrderByDescending(r => r.Id).Take(100).ToListAsync();
        Names = Employees.ToDictionary(x => x.Id, x => x.FullName);
        Devices = await db.ZkDevices.ToListAsync();
        var filtered=db.AttendanceDays.AsNoTracking().Where(x => x.Date >= From && x.Date <= To && (!EmployeeFilter.HasValue || x.EmployeeId==EmployeeFilter));
        Paging=new(RowsPerPage,PageNumber,await filtered.CountAsync());RowsPerPage=Paging.RowsPerPage;PageNumber=Paging.PageNumber;
        var page=filtered.OrderByDescending(x=>x.Date).ThenBy(x=>x.EmployeeId).ThenBy(x=>x.Id).Skip(Paging.Skip).AsQueryable();
        if(Paging.Size.HasValue)page=page.Take(Paging.Size.Value);
        Items=await page.ToListAsync();
        if (!db.Scoped)
            Unmapped = await db.ZkPunchs.Where(x => !db.Employees.Any(e => e.ZkPin == x.Pin)).Select(x => x.Pin).Distinct().ToListAsync();
    }

    public async Task<IActionResult> OnGetTodayPunchesAsync()
    {
        Response.Headers.CacheControl = "no-store";
        return Partial("_TodayPunches", await LoadTodayPunchesAsync());
    }

    private Task<List<PunchRow>> LoadTodayPunchesAsync()
    {
        var today = BusinessClock.Today;
        var tomorrow = today.AddDays(1);
        var scoped = db.Scoped;
        return (from punch in db.ZkPunchs
                where punch.PunchTime >= today && punch.PunchTime < tomorrow
                join employee in db.Employees on punch.Pin equals employee.ZkPin into matches
                from employee in matches.DefaultIfEmpty()
                where (!scoped || employee != null) && (!EmployeeFilter.HasValue || (employee != null && employee.Id == EmployeeFilter))
                join device in db.ZkDevices on punch.DeviceId equals device.Id into terminals
                from device in terminals.DefaultIfEmpty()
                orderby punch.PunchTime descending, punch.Id descending
                select new PunchRow(punch.PunchTime, punch.Pin, employee == null ? null : employee.FullName,
                    device == null ? null : device.Name)).Take(200).ToListAsync();
    }

    private async Task<IActionResult> Attempt(Func<Task> work)
    {
        try
        {
            await work();
            return RedirectToPage();
        }
        catch (Exception e)when (e is InvalidOperationException or FormatException)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", e.Message);
            await OnGetAsync();
            return Page();
        }
    }

    private async Task<DateTime> LocalTime(string pin, string timestamp)
    {
        if (!DateTime.TryParse(timestamp, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var local))
            throw new FormatException("Invalid timestamp.");
        if (timestamp.EndsWith("Z") || System.Text.RegularExpressions.Regex.IsMatch(timestamp, @"[+-]\d{2}:\d{2}$"))
        {
            if (!DateTimeOffset.TryParse(timestamp, out var offset))
                throw new FormatException("Invalid timestamp offset.");
            var e = await db.Employees.SingleOrDefaultAsync(x => x.ZkPin == pin) ?? throw new InvalidOperationException("Map the PIN before importing offset timestamps.");
            local = TimeZoneInfo.ConvertTime(offset, TimeZoneInfo.FindSystemTimeZoneById("Africa/Mogadishu")).DateTime;
        }

        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    public Task<IActionResult> OnPostPunchAsync(int deviceId, string pin, string timestamp, string reason) => Attempt(async () => await attendance.ImportAsync(deviceId, pin, await LocalTime(pin, timestamp), User.Identity!.Name!, reason));
    public Task<IActionResult> OnPostPunchAllAsync(int deviceId, string reason) => Attempt(async () =>
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Enter a reason for the bulk punch.");

        var now = BusinessClock.Now;
        var timestamp = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        var date = timestamp.Date;
        var activeEmployees = db.Employees.Where(e => e.Status == "Active" && e.HireDate <= date &&
            (e.TerminationDate == null || e.TerminationDate >= date));
        var pins = await activeEmployees.Where(e => e.ZkPin != null && e.ZkPin != "")
            .OrderBy(e => e.Id).Select(e => e.ZkPin!).ToListAsync();
        if (pins.Count == 0)
            throw new InvalidOperationException("There are no active employees with a terminal User ID to punch.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var pin in pins)
            await attendance.ImportAsync(deviceId, pin, timestamp, User.Identity!.Name!, reason.Trim());
        await transaction.CommitAsync();

        var skipped = await activeEmployees.CountAsync(e => e.ZkPin == null || e.ZkPin == "");
        TempData["BulkPunchNotice"] = $"Processed punches for {pins.Count} employees at {timestamp:HH:mm:ss} Mogadishu time."
            + (skipped > 0 ? $" Skipped {skipped} without a terminal User ID." : "");
    });

    public async Task<IActionResult> OnPostReviewClockAsync(int id, string resolution, DateTime? correctedTime, string reason)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("HR")) return Forbid();
        return await Attempt(async () =>
        {
            if (!ModelState.IsValid || resolution is not ("correct" or "ignore") || string.IsNullOrWhiteSpace(reason) || reason.Length > 500 ||
                resolution == "correct" && (correctedTime == null || correctedTime.Value.Year < 2000 || correctedTime > BusinessClock.Now.AddMinutes(15)))
                throw new InvalidOperationException("Choose a resolution and provide a reason. Corrections require the confirmed actual time in EAT, not a future time.");
            await using var tx = await db.Database.BeginTransactionAsync();
            var row = await VisibleClockReviews().AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);
            if (row == null) throw new InvalidOperationException("Clock exception not found in your scope.");
            var time = resolution == "correct" ? DateTime.SpecifyKind(correctedTime!.Value, DateTimeKind.Unspecified) : (DateTime?)null;
            var result = resolution == "correct" ? "Corrected" : "Ignored";
            var claimed = await db.ZkPunchReviews.Where(r => r.Id == id && r.ReviewedUtc == null).ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ReviewedUtc, DateTime.UtcNow).SetProperty(r => r.CorrectedTime, time)
                .SetProperty(r => r.Resolution, result).SetProperty(r => r.ReviewReason, reason.Trim()).SetProperty(r => r.Reviewer, User.Identity!.Name));
            if (claimed == 0) { TempData["BulkPunchNotice"] = "This exception has already been reviewed."; return; }
            var needsSetup = false;
            if (time.HasValue)
            {
                if (!await db.ZkPunchs.AnyAsync(p => p.DeviceId == row.DeviceId && p.Pin == row.Pin && p.PunchTime == time.Value))
                { db.ZkPunchs.Add(new() { DeviceId = row.DeviceId, Pin = row.Pin, PunchTime = time.Value }); await db.SaveChangesAsync(); }
                var employee = await db.Employees.SingleOrDefaultAsync(e => e.ZkPin == row.Pin);
                if (employee == null) needsSetup = true;
                else
                {
                    try { await attendance.RebuildAsync(employee.Id, await attendance.WorkDateAsync(employee.Id, time.Value)); }
                    catch (InvalidOperationException ex) when (ex.Message.StartsWith("No effective assignment for employee ", StringComparison.Ordinal) || ex.Message == "Configure an effective work rule first.") { needsSetup = true; }
                }
            }
            db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Clock review {id}: {result}; device {row.DeviceId}; original {row.RecordedTime:yyyy-MM-dd HH:mm:ss}; confirmed {time:yyyy-MM-dd HH:mm:ss}; {reason.Trim()}" });
            await db.SaveChangesAsync();
            var pending = await db.ZkPunchReviews.AnyAsync(r => r.DeviceId == row.DeviceId && r.ReviewedUtc == null);
            if (!pending) await db.ZkDevices.Where(d => d.Id == row.DeviceId && d.LastError != null && d.LastError.Contains("clock errors"))
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastError, (string?)null));
            await tx.CommitAsync();
            TempData["BulkPunchNotice"] = result + ". Original terminal timestamp retained in the review history." + (needsSetup ? " Map the PIN or configure work rules, then rebuild attendance." : "");
        });
    }

    private const string PunchColumns = "DeviceId,Pin,Timestamp";

    public IActionResult OnGetSample() =>
        RegistrationCsv.Download("attendance-punches-sample", PunchColumns, [[1, "101", BusinessClock.Today.AddHours(8)]]);

    public async Task<IActionResult> OnGetExportAsync()
    {
        if (!ModelState.IsValid || To < From || (To - From).TotalDays > 366 || To.Date == DateTime.MaxValue.Date)
            return BadRequest("Choose a valid range of up to 367 days.");
        var end = To.Date.AddDays(1);
        var scoped = db.Scoped;
        var punches = await db.ZkPunchs.AsNoTracking().Where(p => p.PunchTime >= From.Date && p.PunchTime < end &&
            (!scoped || db.Employees.Any(e => e.ZkPin == p.Pin)) &&
            (!EmployeeFilter.HasValue || db.Employees.Any(e => e.Id == EmployeeFilter && e.ZkPin == p.Pin))).OrderBy(p => p.PunchTime).ThenBy(p => p.Id).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("attendance-punches", PunchColumns,
            punches.Select(p => new object?[] { p.DeviceId, p.Pin, p.PunchTime }));
    }

    public Task<IActionResult> OnPostCsvAsync(IFormFile upload, string reason) => Attempt(async () =>
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Provide an import reason.");
        await RegistrationCsv.ImportAsync(this, db, upload, PunchColumns, async row =>
        {
            var pin = row.Get<string>("Pin");
            if (string.IsNullOrWhiteSpace(pin))
                throw new FormatException("Enter a terminal PIN.");
            try
            {
                await attendance.ImportAsync(row.Get<int>("DeviceId"), pin, await LocalTime(pin, row.Get<string>("Timestamp")),
                    User.Identity!.Name!, reason);
            }
            catch (InvalidOperationException ex)
            {
                throw new FormatException(ex.Message, ex);
            }
            return RedirectToPage();
        }, maxRecords: 20000);
    });

    public Task<IActionResult> OnPostCorrectAsync(int employeeId, DateTime date, DateTime first, DateTime last, string reason) => Attempt(async () =>
    {
        if (string.IsNullOrWhiteSpace(reason) || first >= last || (last - first).TotalHours > 24)
            throw new InvalidOperationException("Enter valid punches and a correction reason.");
        var rule = await calendar.RuleAsync(employeeId, date);
        var window = CalendarService.Window(rule, date);
        if (first.Date < date.Date || first > window.End || last < window.Start)
            throw new InvalidOperationException("Punches must belong to the selected work date.");
        var day = await db.AttendanceDays.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == date);
        if (day == null)
        {
            day = new()
            {
                EmployeeId = employeeId,
                Date = date
            };
            db.AttendanceDays.Add(day);
        }

        if (rule.UsePunchWindows && (!AttendanceMinutes.CheckIn(rule, first) || !AttendanceMinutes.CheckOut(rule, last) || last.Date != date.Date))
            throw new InvalidOperationException("Use punches within the configured check-in and checkout windows (EAT) for this work date.");
        day.FirstIn = first;
        day.LastOut = last;
        day.WorkedHours = (decimal)Math.Max(0, (last - first).TotalHours - rule.BreakMinutes / 60d);
        day.ExpectedHours = rule.DailyHours;
        day.LateMinutes = Math.Max(0, (int)(first - window.Start).TotalMinutes - rule.GraceMinutes);
        day.EarlyMinutes = Math.Max(0, (int)(window.End - last).TotalMinutes);
        day.Status = day.LateMinutes > 0 ? "Late" : "Present";
        day.IsCorrected = true;
        day.CorrectionReason = reason;
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Attendance correction {employeeId}/{date:yyyy-MM-dd}: {reason}" });
        await db.SaveChangesAsync();
        await attendance.RebuildAsync(employeeId, date);
    });
    public Task<IActionResult> OnPostOvertimeAsync(int employeeId, DateTime date, decimal hours, string reason) => Attempt(async () =>
    {
        if (!User.IsInRole("Management") && !User.IsInRole("Admin"))
            throw new InvalidOperationException("Management approves overtime.");
        if (hours < 0 || hours > 16 || string.IsNullOrWhiteSpace(reason) || !await db.Employees.AnyAsync(x => x.Id == employeeId))
            throw new InvalidOperationException("Invalid overtime approval.");
        var o = await db.OvertimeApprovals.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == date);
        if (o == null)
        {
            o = new()
            {
                EmployeeId = employeeId,
                Date = date
            };
            db.OvertimeApprovals.Add(o);
        }

        o.Hours = hours;
        o.Reason = reason;
        o.Actor = User.Identity!.Name!;
        db.AuditLogs.Add(new() { Actor = o.Actor, Action = $"Overtime {employeeId}/{date:yyyy-MM-dd}: {hours}; {reason}" });
        await db.SaveChangesAsync();
    });
    public Task<IActionResult> OnPostMapAsync(int employeeId, string pin) => Attempt(async () =>
    {
        if (await db.Employees.AnyAsync(x => x.Id != employeeId && x.ZkPin == pin))
            throw new InvalidOperationException("PIN is already mapped.");
        var e = await db.Employees.SingleAsync(x => x.Id == employeeId);
        e.ZkPin = pin;
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Mapped PIN {pin} to employee {employeeId}" });
        await db.SaveChangesAsync();
        var dates = await db.ZkPunchs.Where(x => x.Pin == pin).Select(x => x.PunchTime.Date).Distinct().ToListAsync();
        foreach (var d in dates)
            if (d >= e.HireDate)
                await attendance.RebuildAsync(e.Id, d);
    });
    public Task<IActionResult> OnPostRebuildAsync(DateTime from, DateTime to) => Attempt(async () =>
    {
        if (from > to || (to - from).TotalDays > 62)
            throw new InvalidOperationException("Select up to 63 days.");
        foreach (var e in await db.Employees.Where(x => x.HireDate <= to).ToListAsync())
            for (var d = from; d <= to; d = d.AddDays(1))
                if (d >= e.HireDate && (e.TerminationDate == null || d <= e.TerminationDate))
                    await attendance.RebuildAsync(e.Id, d);
    });
}
