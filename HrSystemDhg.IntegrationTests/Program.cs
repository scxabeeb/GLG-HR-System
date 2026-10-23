using System.Security.Claims;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

var passed = 0;
void Check(bool result, string name)
{
    if (!result)
        throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS " + name);
    passed++;
}

async Task Reject(Func<Task> action, string name)
{
    try
    {
        await action();
    }
    catch (InvalidOperationException)
    {
        Check(true, name);
        return;
    }

    throw new Exception("FAILED: " + name);
}

Check(PayrollPolicy.DayDeductions(100, true, true, true, 120, 1, 1) == (0m, 0m), "paid leave suppresses attendance deductions");
Check(PayrollPolicy.DayDeductions(100, true, false, true, 120, 1, 1) == (0m, 100m), "unpaid leave deducts once");
var night = CalendarService.Window(new WorkRule { StartHour = 22, DailyHours = 8, BreakMinutes = 0 }, new DateTime(2026, 10, 5));
Check(night.End == new DateTime(2026, 10, 6, 6, 0, 0), "overnight shift window crosses midnight");
Check(CalendarService.LeaveFraction(new() { StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) }, new() { DailyHours = 8 }) == .5m, "partial-day leave fraction");
Check(ExportService.Safe("=SUM(A1:A5)").StartsWith("'"), "CSV formula injection is neutralized");
using (var zip = new ZipArchive(new MemoryStream(ExportService.Excel(new[] { new[] { "Name", "Value" }, new[] { "Somali / العربية", "=1+1" } }))))
{
    Check(zip.GetEntry("xl/worksheets/sheet1.xml") != null, "Excel workbook contains worksheet");
    using var sr = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
    Check(!(await sr.ReadToEndAsync()).Contains("<f>"), "Excel values are stored as text, not formulas");
}

var cs = Environment.GetEnvironmentVariable("DHG_TEST_CONNECTION");
if (string.IsNullOrWhiteSpace(cs))
{
    Console.WriteLine($"{passed} checks passed. Set DHG_TEST_CONNECTION to run MySQL integration checks.");
    return;
}

if (!cs.Contains("dhg_integration", StringComparison.OrdinalIgnoreCase))
    throw new Exception("Integration database must be named dhg_integration.");
var options = new DbContextOptionsBuilder<HrDbContext>().UseMySql(cs, new MySqlServerVersion(new Version(8, 0, 0))).Options;
await using var db = new HrDbContext(options);
await db.Database.MigrateAsync();
var suffix = Guid.NewGuid().ToString("N")[..8];
var b = new Branch
{
    Name = "Integration " + suffix,
    Code = suffix
};
var b2 = new Branch
{
    Name = "Other " + suffix,
    Code = "B" + suffix
};
db.Branchs.AddRange(b, b2);
await db.SaveChangesAsync();
var d = new Department
{
    Name = "Test",
    Code = "T",
    BranchId = b.Id
};
db.Departments.Add(d);
await db.SaveChangesAsync();
var e = new Employee
{
    EmployeeNumber = suffix,
    FullName = "Integration employee",
    WorkEmail = suffix + "@test.local",
    HireDate = new(2026, 10, 1),
    ZkPin = suffix
};
db.Employees.Add(e);
await db.SaveChangesAsync();
db.EmployeeAssignments.Add(new() { EmployeeId = e.Id, BranchId = b.Id, DepartmentId = d.Id, EffectiveFrom = e.HireDate, JobTitle = "Tester" });
db.WorkRules.Add(new() { EffectiveFrom = new(2026, 1, 1), BranchId = b.Id, DailyHours = 8, StartHour = 8, BreakMinutes = 0, WorkingWeekdays = "1,2,3,4,5" });
db.DeductionRules.Add(new() { EffectiveFrom = new(2026, 1, 1), BranchId = b.Id, AbsenceMultiplier = 1, LatePerMinute = 1 });
db.SalaryVersions.Add(new() { EmployeeId = e.Id, Basic = 2200, EffectiveFrom = e.HireDate, Currency = "USD" });
var paid = new LeaveType
{
    Name = "Paid " + suffix,
    AnnualDays = 30
};
var unpaid = new LeaveType
{
    Name = "Unpaid " + suffix,
    IsPaid = false,
    EnforceBalance = false
};
db.LeaveTypes.AddRange(paid, unpaid);
var device = new ZkDevice
{
    Name = "Manual " + suffix
};
db.ZkDevices.Add(device);
await db.SaveChangesAsync();
var calendar = new CalendarService(db);
var attendance = new AttendanceService(db, calendar);
var leaves = new LeaveService(db, calendar);
var payroll = new PayrollService(db, calendar, attendance);
var r1 = new LeaveRequest
{
    EmployeeId = e.Id,
    LeaveTypeId = paid.Id,
    From = new(2026, 10, 5),
    To = new(2026, 10, 5),
    Reason = "Paid test"
};
await leaves.SubmitAsync(r1, "test");
await Reject(() => leaves.SubmitAsync(new() { EmployeeId = e.Id, LeaveTypeId = paid.Id, From = r1.From, To = r1.To, Reason = "overlap" }, "test"), "overlapping leave blocked");
db.ChangeTracker.Clear();
await leaves.DecideAsync(r1.Id, "Approved", "test", "test", null, true);
Check(await leaves.RemainingAsync(e.Id, paid.Id, 2026) == 29, "approval consumes one working day");
var r2 = new LeaveRequest
{
    EmployeeId = e.Id,
    LeaveTypeId = unpaid.Id,
    From = new(2026, 10, 6),
    To = new(2026, 10, 6),
    Reason = "Unpaid test"
};
await leaves.SubmitAsync(r2, "test");
await leaves.DecideAsync(r2.Id, "Approved", "test", "test", null, true);
await attendance.RebuildAsync(e.Id, r1.From);
Check((await db.AttendanceDays.SingleAsync(x => x.EmployeeId == e.Id && x.Date == r1.From)).Status == "On leave", "approved leave appears in daily attendance");
await attendance.ImportAsync(device.Id, suffix, new(2026, 10, 7, 8, 30, 0), "test", "import");
await attendance.ImportAsync(device.Id, suffix, new(2026, 10, 7, 16, 0, 0), "test", "import");
await attendance.ImportAsync(device.Id, suffix, new(2026, 10, 7, 16, 0, 0), "test", "duplicate");
Check(await db.ZkPunchs.CountAsync(x => x.DeviceId == device.Id && x.Pin == suffix) == 2, "raw punch import is idempotent");
var p = new PayrollPeriod
{
    From = new(2026, 10, 5),
    To = new(2026, 10, 9),
    WorkingDays = 5,
    BranchId = b.Id
};
db.PayrollPeriods.Add(p);
await db.SaveChangesAsync();
await payroll.CalculateAsync(p.Id, true, "test");
var line = await db.PayrollLines.SingleAsync(x => x.PayrollPeriodId == p.Id && x.Revision == 1);
await using (var fresh = new HrDbContext(options))
{
    var stored = await fresh.PayrollPeriods.SingleAsync(x => x.Id == p.Id);
    var freshPayroll = new PayrollService(fresh, new CalendarService(fresh), new AttendanceService(fresh, new CalendarService(fresh)));
    Check(stored.SourceHash == await freshPayroll.HashAsync(stored), "payroll fingerprint survives a fresh request and database decimal normalization");
}

Check(line.UnpaidLeave == 440, "unpaid leave prorated once");
var deductions = await db.PayrollDeductions.Where(x => x.PayrollLineId == line.Id).ToListAsync();
Check(!deductions.Any(x => x.Date == r1.From || x.Date == r2.From), "paid and unpaid leave have no attendance deduction");
Check(deductions.Any(x => x.Date == new DateTime(2026, 10, 7) && x.Calculated == 20), "late deduction applies after grace");
Check(line.Net == line.Gross - line.UnpaidLeave - line.AttendanceApplied, "net matches gross minus applied deductions and leave");
await payroll.AdjustAsync(deductions[0].Id, 0, "waive test", "test");
await payroll.DecideAsync(p.Id, "Review", "test");
var rule = await db.DeductionRules.SingleAsync(x => x.BranchId == b.Id);
rule.LatePerMinute = 2;
await db.SaveChangesAsync();
await Reject(() => payroll.DecideAsync(p.Id, "Lock", "test"), "source changes block locking reviewed payroll");
db.ChangeTracker.Clear();
await payroll.DecideAsync(p.Id, "Reopen", "test", "source changed");
await payroll.CalculateAsync(p.Id, false, "test");
await payroll.DecideAsync(p.Id, "Review", "test");
await payroll.DecideAsync(p.Id, "Lock", "test");
await payroll.PublishAsync(p.Id, "test");
var current = await db.PayrollLines.SingleAsync(x => x.PayrollPeriodId == p.Id && x.Revision == 2);
Check(current.AttendanceApplied == 0 && current.AttendanceCalculated > 0, "attendance can be waived while retaining calculated amounts");
Check(current.Net == current.Gross - current.UnpaidLeave, "waiving attendance does not waive unpaid leave");
var lockedNet = current.Net;
await attendance.ImportAsync(device.Id, suffix, new(2026, 10, 9, 8, 0, 0), "test", "late arrival after lock");
Check((await db.PayrollLines.AsNoTracking().SingleAsync(x => x.Id == current.Id)).Net == lockedNet, "late punches never mutate locked payroll");
Check(await db.PayrollLines.CountAsync(x => x.PayrollPeriodId == p.Id) == 2, "payroll recalculation retains revision history");
await Reject(() => payroll.CalculateAsync(p.Id, true, "test"), "locked payroll rejects recalculation");
db.ChangeTracker.Clear();
// Holidays and partial-day unpaid leave must never double deduct.
db.SalaryVersions.Add(new() { EmployeeId = e.Id, Basic = 3300, EffectiveFrom = new(2026, 10, 12), Currency = "USD" });
db.Holidays.Add(new() { BranchId = b.Id, Date = new(2026, 10, 12), Name = "Test holiday" });
await db.SaveChangesAsync();
var half = new LeaveRequest
{
    EmployeeId = e.Id,
    LeaveTypeId = unpaid.Id,
    From = new(2026, 10, 13),
    To = new(2026, 10, 13),
    StartTime = TimeSpan.FromHours(8),
    EndTime = TimeSpan.FromHours(12),
    Reason = "Half day test"
};
await leaves.SubmitAsync(half, "test");
await leaves.DecideAsync(half.Id, "Approved", "test", "test", null, true);
var p2 = new PayrollPeriod
{
    From = new(2026, 10, 12),
    To = new(2026, 10, 16),
    WorkingDays = 4,
    BranchId = b.Id
};
db.PayrollPeriods.Add(p2);
await db.SaveChangesAsync();
await payroll.CalculateAsync(p2.Id, true, "test");
var second = await db.PayrollLines.SingleAsync(x => x.PayrollPeriodId == p2.Id);
var partial = await db.PayrollDeductions.SingleAsync(x => x.PayrollLineId == second.Id && x.Date == half.From);
Check(second.Basic == 3300, "payroll uses the salary effective at period end");
Check(second.UnpaidLeave == 412.5m && partial.Calculated == 412.5m, "half-day unpaid leave and half-day absence together deduct one daily rate");
Check(!await db.PayrollDeductions.AnyAsync(x => x.PayrollLineId == second.Id && x.Date == new DateTime(2026, 10, 12)), "public holidays do not create absence deductions");
Check((await db.AttendanceDays.SingleAsync(x => x.EmployeeId == e.Id && x.Date == half.From)).Status == "On leave (partial)", "partial leave is visible in attendance");
await payroll.OverrideLineAsync(second.Id, 25, "Integration net override", "test");
Check((await db.PayrollLines.SingleAsync(x => x.Id == second.Id)).Net == 25, "reasoned net override is applied before review");
await payroll.OverrideLineAsync(second.Id, null, "Restore calculated formula", "test");
Check((await db.PayrollLines.SingleAsync(x => x.Id == second.Id)).Net == 0, "clearing override restores the calculated net");
await Reject(() => payroll.OverrideLineAsync(current.Id, 25, "Locked override attempt", "test"), "locked payroll rejects net overrides");
db.ChangeTracker.Clear();
await Reject(() => leaves.SubmitAsync(new() { EmployeeId = e.Id, LeaveTypeId = paid.Id, From = new(2026, 10, 14), To = new(2026, 10, 14), StartTime = TimeSpan.FromHours(18), EndTime = TimeSpan.FromHours(20), Reason = "outside shift" }, "test"), "leave outside shift hours is rejected");
db.ChangeTracker.Clear();
db.WorkRules.Add(new() { BranchId = b.Id, EffectiveFrom = new(2026, 10, 19), StartHour = 22, DailyHours = 8, BreakMinutes = 0, WorkingWeekdays = "1,2,3,4,5" });
await db.SaveChangesAsync();
await attendance.ImportAsync(device.Id, suffix, new(2026, 10, 19, 22, 15, 0), "test", "night in");
await attendance.ImportAsync(device.Id, suffix, new(2026, 10, 20, 6, 0, 0), "test", "night out");
var overnight = await db.AttendanceDays.SingleAsync(x => x.EmployeeId == e.Id && x.Date == new DateTime(2026, 10, 19));
Check(overnight.LastOut == new DateTime(2026, 10, 20, 6, 0, 0) && overnight.LateMinutes == 5, "overnight punches map to their shift work date");
var nightLeave = new LeaveRequest
{
    EmployeeId = e.Id,
    LeaveTypeId = paid.Id,
    From = new(2026, 10, 20),
    To = new(2026, 10, 20),
    StartTime = TimeSpan.FromHours(22),
    EndTime = TimeSpan.FromHours(2),
    Reason = "night partial"
};
await leaves.SubmitAsync(nightLeave, "test");
Check(await calendar.LeaveDaysAsync(nightLeave) == .5m, "partial leave can span midnight within an overnight shift");
nightLeave.Status = "Cancelled";
await db.SaveChangesAsync();
var user = new ApplicationUser
{
    Id = "test-" + suffix,
    UserName = suffix,
    Email = suffix + "@test.local",
    EmployeeId = e.Id
};
db.Users.Add(user);
db.UserBranchScopes.Add(new() { UserId = user.Id, BranchId = b2.Id });
await db.SaveChangesAsync();
var currentAssignment = await db.EmployeeAssignments.SingleAsync(x => x.EmployeeId == e.Id && x.EffectiveTo == null);
currentAssignment.EffectiveTo = BusinessClock.Today.AddDays(9);
var futureDepartment = new Department
{
    Name = "Future department",
    BranchId = b2.Id,
    Code = "F" + suffix
};
db.Departments.Add(futureDepartment);
await db.SaveChangesAsync();
db.EmployeeAssignments.Add(new() { EmployeeId = e.Id, BranchId = b2.Id, DepartmentId = futureDepartment.Id, EffectiveFrom = BusinessClock.Today.AddDays(10), JobTitle = "Future assignment" });
await db.SaveChangesAsync();
var http = new HttpContextAccessor
{
    HttpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Role, "HR") }, "test"))
    }
};
await using (var scoped = new HrDbContext(options, http))
{
    Check(!await scoped.Employees.AnyAsync(x => x.Id == e.Id), "HR branch scope hides employees in other branches");
    Check(!await scoped.SalaryVersions.AnyAsync(x => x.EmployeeId == e.Id), "HR branch scope hides confidential salary rows");
}

http.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Role, "Employee") }, "test"));
await using (var own = new HrDbContext(options, http))
{
    Check(await own.Employees.CountAsync() == 1, "employee query filters expose only the linked employee");
    Check(await own.PayrollLines.AllAsync(x => x.EmployeeId == e.Id), "employee payroll is limited to own records");
}

await PasswordRecoveryChecks.RunAsync(cs,Check);
Console.WriteLine($"{passed} checks passed, including MySQL integration.");
