using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.EntityFrameworkCore;

public static class AttendanceMinuteChecks
{
    public static async Task RunAsync(DbContextOptions<HrDbContext> options, Action<bool, string> check)
    {
        await using var db = new HrDbContext(options);
        var date = new DateTime(2026, 11, 23);
        var key = Guid.NewGuid().ToString("N")[..8];
        var branch = new Branch { Name = "Minute checks", Code = key };
        var employee = new Employee { FullName = "Minute fixture", EmployeeNumber = key, ZkPin = key, HireDate = date, RegistrationComplete = true };
        var device = new ZkDevice { Name = "Minute fixture", Endpoint = "fixture" };
        db.AddRange(branch, employee, device); await db.SaveChangesAsync();
        var department = new Department { Name = "Minute checks", Code = key, BranchId = branch.Id };
        db.Add(department); await db.SaveChangesAsync();
        db.EmployeeAssignments.Add(new() { EmployeeId = employee.Id, BranchId = branch.Id, DepartmentId = department.Id, EffectiveFrom = date });
        db.WorkRules.Add(new() { BranchId = branch.Id, EffectiveFrom = date, UsePunchWindows = true, DailyHours = 8, StartHour = 6, StartMinute = 30, BreakMinutes = 0, GraceMinutes = 0, WorkingWeekdays = "0,1,2,3,4,6" });
        db.SalaryVersions.Add(new() { EmployeeId = employee.Id, EffectiveFrom = date, Basic = 1500, IsDraft = false });
        db.ZkPunchs.AddRange(new() { DeviceId = device.Id, Pin = key, PunchTime = date.AddHours(10.5) }, new() { DeviceId = device.Id, Pin = key, PunchTime = date.AddHours(15.5) });
        await db.SaveChangesAsync();
        var calendar = new CalendarService(db); var attendance = new AttendanceService(db, calendar);
        await attendance.RebuildAsync(employee.Id, date);
        var day = await db.AttendanceDays.SingleAsync(d => d.EmployeeId == employee.Id && d.Date == date);
        check(day.FirstIn == date.AddHours(10.5) && day.LastOut == date.AddHours(15.5) && day.WorkedHours == 5 && day.MissingMinutes == 180, "EAT morning and afternoon punches produce 180 missing minutes");
        var period = new PayrollPeriod { From = date, To = date, BranchId = branch.Id };
        db.Add(period); await db.SaveChangesAsync();
        await new PayrollService(db, calendar, attendance).CalculateAsync(period.Id, true, "minute-fixture");
        var line = await db.PayrollLines.SingleAsync(l => l.PayrollPeriodId == period.Id);
        check(line.AttendanceApplied == 18.75m && line.Net == 1481.25m, "payroll deducts basic / 30 / 480 per missing minute");
        await new PayrollService(db, calendar, attendance).WaiveAttendanceAsync(period.Id, employee.Id, "Fixture waiver", "minute-fixture");
        await db.Entry(line).ReloadAsync();
        check(line.AttendanceApplied == 0 && line.Net == 1500m, "attendance waivers also cover missing-minute deductions");
        var friday = date.AddDays(4);
        await attendance.RebuildAsync(employee.Id, friday);
        var weekend = await db.AttendanceDays.SingleAsync(d => d.EmployeeId == employee.Id && d.Date == friday);
        check(weekend.Status == "Weekend" && weekend.MissingMinutes == 0, "Friday has no absence or missing-minute charge");
        db.ZkPunchs.Add(new() { DeviceId = device.Id, Pin = key, PunchTime = date.AddDays(1).AddHours(15) });
        await db.SaveChangesAsync(); await attendance.RebuildAsync(employee.Id, date.AddDays(1));
        check((await db.AttendanceDays.SingleAsync(d => d.EmployeeId == employee.Id && d.Date == date.AddDays(1))).Status == "Incomplete punch", "checkout-only attendance requires review instead of inventing a check-in");
    }
}
