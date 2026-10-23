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
        Employees = await db.Employees.ToListAsync();
        Names = Employees.ToDictionary(x => x.Id, x => x.FullName);
        Devices = await db.ZkDevices.ToListAsync();
        Items = await db.AttendanceDays.Where(x => x.Date >= From && x.Date <= To).OrderByDescending(x => x.Date).Take(500).ToListAsync();
        if (!db.Scoped)
            Unmapped = await db.ZkPunchs.Where(x => !db.Employees.Any(e => e.ZkPin == x.Pin)).Select(x => x.Pin).Distinct().ToListAsync();
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
            var a = await calendar.AssignmentAsync(e.Id, offset.Date);
            var branch = await db.Branchs.SingleAsync(x => x.Id == a.BranchId);
            local = TimeZoneInfo.ConvertTime(offset, TimeZoneInfo.FindSystemTimeZoneById(branch.Timezone)).DateTime;
        }

        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    public Task<IActionResult> OnPostPunchAsync(int deviceId, string pin, string timestamp, string reason) => Attempt(async () => await attendance.ImportAsync(deviceId, pin, await LocalTime(pin, timestamp), User.Identity!.Name!, reason));
    public Task<IActionResult> OnPostCsvAsync(IFormFile upload, string reason) => Attempt(async () =>
    {
        if (upload == null || upload.Length > 2 * 1024 * 1024 || string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Upload a CSV up to 2 MB and provide a reason.");
        using var reader = new StreamReader(upload.OpenReadStream());
        if ((await reader.ReadLineAsync())?.Trim() != "DeviceId,Pin,Timestamp")
            throw new FormatException("CSV header must be DeviceId,Pin,Timestamp");
        await using var tx = await db.Database.BeginTransactionAsync();
        int count = 0;
        while (await reader.ReadLineAsync()is string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var cells = line.Split(',');
            if (cells.Length != 3 || !int.TryParse(cells[0], out var device))
                throw new FormatException("Invalid CSV row " + (count + 2));
            await attendance.ImportAsync(device, cells[1].Trim(), await LocalTime(cells[1].Trim(), cells[2].Trim()), User.Identity!.Name!, reason);
            if (++count > 20000)
                throw new FormatException("Maximum 20,000 punches per import.");
        }

        await tx.CommitAsync();
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
