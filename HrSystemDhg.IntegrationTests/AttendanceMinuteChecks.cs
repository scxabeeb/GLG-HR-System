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
        check(line.AttendanceApplied == 21.63m && line.Net == 1478.37m, "payroll deducts basic / 26 / 480 per missing minute");
        await new PayrollService(db, calendar, attendance).WaiveAttendanceAsync(period.Id, employee.Id, "Fixture waiver", "minute-fixture");
        await db.Entry(line).ReloadAsync();
        check(line.AttendanceApplied == 0 && line.Net == 1500m, "attendance waivers also cover missing-minute deductions");
        // Regression for the reported 400 -> 92.31 proration: six eligible days in a 26-day month.
        var fullSalaryBranch = new Branch { Name = "Full salary " + key, Code = "FULL" + key };
        db.Branchs.Add(fullSalaryBranch); await db.SaveChangesAsync();
        var fullSalaryDept = new Department { Name = "Full salary", Code = "FULL" + key, BranchId = fullSalaryBranch.Id };
        db.Departments.Add(fullSalaryDept); await db.SaveChangesAsync();
        var hire = new DateTime(2026, 11, 24);
        var fullSalaryEmployee = new Employee { EmployeeNumber = "FULL" + key, FullName = "Full monthly basic fixture", WorkEmail = "full" + key + "@example.test", HireDate = hire, RegistrationComplete = true, ZkPin = "FULL" + key };
        db.Employees.Add(fullSalaryEmployee); await db.SaveChangesAsync();
        db.EmployeeAssignments.Add(new() { EmployeeId = fullSalaryEmployee.Id, BranchId = fullSalaryBranch.Id, DepartmentId = fullSalaryDept.Id, EffectiveFrom = hire });
        db.SalaryVersions.Add(new() { EmployeeId = fullSalaryEmployee.Id, Basic = 400m, EffectiveFrom = hire, IsDraft = false });
        db.WorkRules.Add(new() { BranchId = fullSalaryBranch.Id, EffectiveFrom = new(2026,11,1), DailyHours = 8, BreakMinutes = 0, UsePunchWindows = true, WorkingWeekdays = "0,1,2,3,4,6" });
        for(var d=hire; d<=new DateTime(2026,11,30); d=d.AddDays(1))
        {
            if(d.DayOfWeek==DayOfWeek.Friday)continue;
            db.ZkPunchs.AddRange(new() { DeviceId = device.Id, Pin = fullSalaryEmployee.ZkPin!, PunchTime = d.AddHours(d==hire ? 7.5 : 6.5) },new() { DeviceId = device.Id, Pin = fullSalaryEmployee.ZkPin!, PunchTime = d.AddHours(14.5) });
        }
        var fullSalaryPeriod = new PayrollPeriod { BranchId = fullSalaryBranch.Id, From = new(2026,11,1), To = new(2026,11,30) };
        db.PayrollPeriods.Add(fullSalaryPeriod); await db.SaveChangesAsync();
        var fullSalaryPayroll = new PayrollService(db, calendar, attendance);
        await fullSalaryPayroll.CalculateAsync(fullSalaryPeriod.Id, true, "full-salary-fixture");
        var fullSalaryLine = await db.PayrollLines.SingleAsync(l=>l.PayrollPeriodId==fullSalaryPeriod.Id && l.Revision==1);
        check(fullSalaryLine.Basic==400m && fullSalaryLine.AttendanceApplied==1.92m && fullSalaryLine.Net==398.08m, "partial-month hire retains 400 basic; 60 missing minutes deduct 1.92 using 26 days");
        check(fullSalaryPeriod.AttendanceSalaryDays==26 && await db.SalaryVersions.AnyAsync(v=>v.EmployeeId==fullSalaryEmployee.Id && v.Basic==400m), "26-day run snapshot leaves registered salary unchanged");
        await fullSalaryPayroll.CalculateAsync(fullSalaryPeriod.Id, true, "full-salary-fixture", salaryDays:30);
        var adjustedDaysLine = await db.PayrollLines.SingleAsync(l=>l.PayrollPeriodId==fullSalaryPeriod.Id && l.Revision==2);
        check(adjustedDaysLine.Basic==400m && adjustedDaysLine.AttendanceApplied==1.67m && fullSalaryPeriod.AttendanceSalaryDays==30, "explicit salary-day setting is retained when recalculating without prorating basic");
        // Full-month absence must charge precisely 400 across 26 days, without rounding residue.
        fullSalaryEmployee.HireDate = new(2026,11,1);
        (await db.EmployeeAssignments.SingleAsync(a=>a.EmployeeId==fullSalaryEmployee.Id)).EffectiveFrom = new(2026,11,1);
        (await db.SalaryVersions.SingleAsync(v=>v.EmployeeId==fullSalaryEmployee.Id)).EffectiveFrom = new(2026,11,1);
        db.ZkPunchs.RemoveRange(await db.ZkPunchs.Where(p=>p.Pin==fullSalaryEmployee.ZkPin).ToListAsync());
        await db.SaveChangesAsync();
        await fullSalaryPayroll.CalculateAsync(fullSalaryPeriod.Id,true,"full-month-absence");
        var absentLine = await db.PayrollLines.SingleAsync(l=>l.PayrollPeriodId==fullSalaryPeriod.Id && l.Revision==3);
        check(absentLine.Basic==400m && absentLine.AttendanceApplied==400m && absentLine.Net==0m,"26 absent days deduct exactly the full 400 salary without daily-rounding residue");
        var friday = date.AddDays(4);
        await attendance.RebuildAsync(employee.Id, friday);
        var weekend = await db.AttendanceDays.SingleAsync(d => d.EmployeeId == employee.Id && d.Date == friday);
        check(weekend.Status == "Weekend" && weekend.MissingMinutes == 0, "Friday has no absence or missing-minute charge");
        db.ZkPunchs.Add(new() { DeviceId = device.Id, Pin = key, PunchTime = date.AddDays(1).AddHours(15) });
        await db.SaveChangesAsync(); await attendance.RebuildAsync(employee.Id, date.AddDays(1));
        check((await db.AttendanceDays.SingleAsync(d => d.EmployeeId == employee.Id && d.Date == date.AddDays(1))).Status == "Incomplete punch", "checkout-only attendance requires review instead of inventing a check-in");
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "minute-fixture"), new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin") }, "test"));
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new Microsoft.AspNetCore.Http.FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["WorkingDaysSubmitted"] = "true", ["WorkingDays"] = new[] { "1", "3", "6" } });
        var settings = new HrSystemDhg.Web.Pages.Admin.RulesModel(db) { PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext { HttpContext = context }, Rule = new WorkRule { BranchId = branch.Id, EffectiveFrom = date.AddDays(10), UsePunchWindows = true, CheckInFrom = TimeSpan.FromHours(7), CheckInUntil = TimeSpan.FromHours(11), CheckOutFrom = TimeSpan.FromHours(12), CheckOutUntil = TimeSpan.FromHours(17), DailyHours = 7, BreakMinutes = 30, MinuteRateMode = "Fixed", FixedMinuteRate = .125m, SalaryDivisor = 26 } };
        check(await settings.OnPostAsync() is Microsoft.AspNetCore.Mvc.RedirectToPageResult, "structured settings save an effective version");
        var savedPolicy = await db.WorkRules.AsNoTracking().SingleAsync(r => r.Id == settings.Rule.Id);
        check(savedPolicy.WorkingWeekdays == "1,3,6" && savedPolicy.DailyHours == 7 && savedPolicy.CheckInFrom == TimeSpan.FromHours(7) && savedPolicy.CheckOutUntil == TimeSpan.FromHours(17) && savedPolicy.FixedMinuteRate == .125m && savedPolicy.SalaryDivisor == 26, "settings persist selected days, time windows, hours, divisor and a precise fixed minute rate");
        var invalid = new HrSystemDhg.Web.Pages.Admin.RulesModel(db) { PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext { HttpContext = context }, Rule = new WorkRule { EffectiveFrom = date, UsePunchWindows = true, CheckInFrom = TimeSpan.FromHours(12), CheckInUntil = TimeSpan.FromHours(10) } };
        check(await invalid.OnPostAsync() is Microsoft.AspNetCore.Mvc.RazorPages.PageResult && !invalid.ModelState.IsValid, "settings reject reversed time windows");
        var paid = new LeaveType { Name = "Paid fixture", IsPaid = true };
        db.LeaveTypes.Add(paid); await db.SaveChangesAsync();
        db.LeaveRequests.Add(new() { EmployeeId = employee.Id, LeaveTypeId = paid.Id, From = date.AddDays(1), To = date.AddDays(1), Status = "Approved" });
        await db.SaveChangesAsync(); await attendance.RebuildAsync(employee.Id, date.AddDays(1));
        var approvedDay = await db.AttendanceDays.SingleAsync(d => d.EmployeeId == employee.Id && d.Date == date.AddDays(1));
        check(approvedDay.Status == "On leave" && approvedDay.MissingMinutes == 0, "approved full-day leave is not absence or missing time");
        var partialDate = date.AddDays(2);
        db.LeaveRequests.Add(new() { EmployeeId = employee.Id, LeaveTypeId = paid.Id, From = partialDate, To = partialDate, StartTime = TimeSpan.FromHours(6.5), EndTime = TimeSpan.FromHours(10.5), Status = "Approved" });
        db.ZkPunchs.AddRange(new() { DeviceId = device.Id, Pin = key, PunchTime = partialDate.AddHours(10.5) }, new() { DeviceId = device.Id, Pin = key, PunchTime = partialDate.AddHours(14.5) });
        await db.SaveChangesAsync(); await attendance.RebuildAsync(employee.Id, partialDate);
        var partialDay = await db.AttendanceDays.SingleAsync(d => d.EmployeeId == employee.Id && d.Date == partialDate);
        check(partialDay.MissingMinutes == 0 && partialDay.Status == "On leave (partial)", "approved partial-day leave plus worked hours meets the attendance requirement");
        var leavePeriod = new PayrollPeriod { From = date.AddDays(1), To = partialDate, BranchId = branch.Id };
        db.PayrollPeriods.Add(leavePeriod); await db.SaveChangesAsync();
        await new PayrollService(db, calendar, attendance).CalculateAsync(leavePeriod.Id, true, "minute-fixture");
        var leaveLine = await db.PayrollLines.SingleAsync(l => l.PayrollPeriodId == leavePeriod.Id);
        check(leaveLine.AttendanceApplied == 0 && leaveLine.Net == leaveLine.Gross, "approved full and partial paid leave has no payroll deduction");
    }
}
