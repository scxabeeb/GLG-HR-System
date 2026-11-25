using Microsoft.Extensions.DependencyInjection;
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
        db.WorkRules.Add(new() { BranchId = branch.Id, EffectiveFrom = date, UsePunchWindows = true, CreditFullDayForValidPunches = false, DailyHours = 8, StartHour = 6, StartMinute = 30, BreakMinutes = 0, GraceMinutes = 0, WorkingWeekdays = "0,1,2,3,4,6" });
        db.SalaryVersions.Add(new() { EmployeeId = employee.Id, EffectiveFrom = date, Basic = 1500, IsDraft = false });
        db.ZkPunchs.AddRange(new() { DeviceId = device.Id, Pin = key, PunchTime = date.AddHours(10.5) }, new() { DeviceId = device.Id, Pin = key, PunchTime = date.AddHours(15.5) });
        await db.SaveChangesAsync();
        var calendar = new CalendarService(db); var attendance = new AttendanceService(db, calendar, new ClosingTestClock(new DateTimeOffset(2026,12,1,0,0,0,TimeSpan.Zero)));
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
        check(await db.PayrollDeductions.AnyAsync(d=>d.PayrollLineId==line.Id && d.Reason!=null && d.Reason.Contains("basic ÷ 26") && d.Reason.Contains("Fixture waiver")), "waiver preserves the original deduction formula for the breakdown table");
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
        db.WorkRules.Add(new() { BranchId = fullSalaryBranch.Id, EffectiveFrom = new(2026,11,1), DailyHours = 8, BreakMinutes = 0, UsePunchWindows = true, CreditFullDayForValidPunches = false, WorkingWeekdays = "0,1,2,3,4,6" });
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
        // The new policy credits any valid window pair, independently of elapsed time.
        var creditDate = new DateTime(2026,11,24);
        var creditBranch = new Branch { Name = "Window credit " + key, Code = "CREDIT" + key };
        db.Branchs.Add(creditBranch);await db.SaveChangesAsync();
        var creditDept = new Department { Name = "Window credit", Code = "CREDIT" + key, BranchId = creditBranch.Id };
        db.Departments.Add(creditDept);await db.SaveChangesAsync();
        var creditEmployee = new Employee { EmployeeNumber="CREDIT"+key, FullName="Window credit fixture", WorkEmail="credit"+key+"@example.test", HireDate=creditDate, RegistrationComplete=true, ZkPin="CREDIT"+key };
        db.Employees.Add(creditEmployee);await db.SaveChangesAsync();
        db.EmployeeAssignments.Add(new(){EmployeeId=creditEmployee.Id,BranchId=creditBranch.Id,DepartmentId=creditDept.Id,EffectiveFrom=creditDate});
        db.SalaryVersions.Add(new(){EmployeeId=creditEmployee.Id,Basic=400m,EffectiveFrom=creditDate,IsDraft=false});
        var creditRule = new WorkRule { BranchId=creditBranch.Id,EffectiveFrom=creditDate,UsePunchWindows=true,CreditFullDayForValidPunches=true,DailyHours=8,BreakMinutes=0,IncompletePolicy="Absent",WorkingWeekdays="0,1,2,3,4,6" };
        db.WorkRules.Add(creditRule);
        db.ZkPunchs.AddRange(new(){DeviceId=device.Id,Pin=creditEmployee.ZkPin!,PunchTime=creditDate.AddHours(10.5)},new(){DeviceId=device.Id,Pin=creditEmployee.ZkPin!,PunchTime=creditDate.AddHours(12.5)});
        var creditPeriod = new PayrollPeriod {BranchId=creditBranch.Id,From=creditDate,To=creditDate};db.PayrollPeriods.Add(creditPeriod);await db.SaveChangesAsync();
        await attendance.RebuildAsync(creditEmployee.Id,creditDate);
        var creditedDay=await db.AttendanceDays.SingleAsync(d=>d.EmployeeId==creditEmployee.Id && d.Date==creditDate);
        check(creditedDay.WorkedHours==8m && creditedDay.MissingMinutes==0 && creditedDay.Status=="Present" && creditedDay.FirstIn==creditDate.AddHours(10.5) && creditedDay.LastOut==creditDate.AddHours(12.5),"10:30 check-in and 12:30 checkout credit eight hours while retaining actual times");
        await fullSalaryPayroll.CalculateAsync(creditPeriod.Id,true,"window-credit");
        var creditLine=await db.PayrollLines.SingleAsync(l=>l.PayrollPeriodId==creditPeriod.Id && l.Revision==1);
        check(creditLine.Basic==400m && creditLine.AttendanceApplied==0m && creditLine.Net==400m,"valid window pair causes no attendance deduction despite two elapsed hours");
        db.ZkPunchs.AddRange(new(){DeviceId=device.Id,Pin=creditEmployee.ZkPin!,PunchTime=creditDate.AddHours(6.5)},new(){DeviceId=device.Id,Pin=creditEmployee.ZkPin!,PunchTime=creditDate.AddHours(15.5)});await db.SaveChangesAsync();
        await attendance.RebuildAsync(creditEmployee.Id,creditDate);
        check(creditedDay.WorkedHours==8m && creditedDay.FirstIn==creditDate.AddHours(6.5) && creditedDay.LastOut==creditDate.AddHours(15.5),"multiple scans retain earliest valid check-in and latest valid checkout with eight-hour credit");
        db.ZkPunchs.RemoveRange(await db.ZkPunchs.Where(p=>p.Pin==creditEmployee.ZkPin && p.PunchTime<creditDate.AddHours(11)).ToListAsync());await db.SaveChangesAsync();
        await attendance.RebuildAsync(creditEmployee.Id,creditDate);
        check(creditedDay.Status=="Absent" && creditedDay.MissingMinutes==480 && creditedDay.StatusMessage!.Contains("check-in missing"),"checkout-only window attendance is absent with a missing check-in reason");
        await fullSalaryPayroll.CalculateAsync(creditPeriod.Id,true,"window-credit");
        creditLine=await db.PayrollLines.SingleAsync(l=>l.PayrollPeriodId==creditPeriod.Id && l.Revision==2);
        check(creditLine.AttendanceApplied==15.38m && creditLine.Net==384.62m,"missing valid check-in deducts one unexcused day using basic divided by 26");
        db.ZkPunchs.RemoveRange(await db.ZkPunchs.Where(p=>p.Pin==creditEmployee.ZkPin).ToListAsync());
        db.ZkPunchs.AddRange(new(){DeviceId=device.Id,Pin=creditEmployee.ZkPin!,PunchTime=creditDate.AddHours(10.5)},new(){DeviceId=device.Id,Pin=creditEmployee.ZkPin!,PunchTime=creditDate.AddHours(12).AddMinutes(29)});await db.SaveChangesAsync();
        await attendance.RebuildAsync(creditEmployee.Id,creditDate);
        check(creditedDay.Status=="Absent" && creditedDay.LastOut==null && creditedDay.StatusMessage!.Contains("check-out missing"),"12:29 checkout is preserved raw but excluded from the 12:30 window");
        var creditLeaveType=new LeaveType{Name="Window paid "+key,IsPaid=true};db.LeaveTypes.Add(creditLeaveType);await db.SaveChangesAsync();
        var creditLeave=new LeaveRequest{EmployeeId=creditEmployee.Id,LeaveTypeId=creditLeaveType.Id,From=creditDate,To=creditDate,Status="Approved",Reason="Window leave fixture"};db.LeaveRequests.Add(creditLeave);await db.SaveChangesAsync();
        await fullSalaryPayroll.CalculateAsync(creditPeriod.Id,true,"window-credit");
        creditLine=await db.PayrollLines.SingleAsync(l=>l.PayrollPeriodId==creditPeriod.Id && l.Revision==3);
        check(creditLine.AttendanceApplied==0m && creditLine.Net==400m && creditedDay.Status=="On leave","approved full paid leave excuses missing punches under window credit");
        creditLeave.StartTime=TimeSpan.FromHours(6.5);creditLeave.EndTime=TimeSpan.FromHours(10.5);
        db.ZkPunchs.Add(new(){DeviceId=device.Id,Pin=creditEmployee.ZkPin!,PunchTime=creditDate.AddHours(12.5)});await db.SaveChangesAsync();
        await fullSalaryPayroll.CalculateAsync(creditPeriod.Id,true,"window-credit");
        creditLine=await db.PayrollLines.SingleAsync(l=>l.PayrollPeriodId==creditPeriod.Id && l.Revision==4);
        check(creditedDay.WorkedHours==4m && creditedDay.MissingMinutes==0 && creditLine.AttendanceApplied==0m && creditLine.Net==400m,"partial paid leave plus valid window pair satisfies the full day without double credit");
        check(await db.WorkRules.AnyAsync(r=>r.BranchId==null && r.EffectiveFrom==new DateTime(2026,11,23) && !r.CreditFullDayForValidPunches) && await db.WorkRules.AnyAsync(r=>r.BranchId==null && r.EffectiveFrom==creditDate && r.CreditFullDayForValidPunches && r.CheckOutFrom==TimeSpan.FromHours(12.5)),"window-credit migration preserves earlier rules and installs the new effective 12:30 checkout rule");
        // A fresh single punch remains pending until the last allowed checkout minute finishes.
        var closingDate = new DateTime(2026,11,25);
        var clock = new ClosingTestClock(new DateTimeOffset(2026,11,25,12,30,59,TimeSpan.Zero)); // 15:30:59 EAT
        var closingAttendance = new AttendanceService(db,calendar,clock);
        db.ZkPunchs.Add(new(){DeviceId=device.Id,Pin=creditEmployee.ZkPin!,PunchTime=closingDate.AddHours(8)});await db.SaveChangesAsync();
        await closingAttendance.RebuildAsync(creditEmployee.Id,closingDate);
        var closingDay=await db.AttendanceDays.SingleAsync(d=>d.EmployeeId==creditEmployee.Id && d.Date==closingDate);
        check(closingAttendance.LocalNow.Hour==15 && closingDay.Status=="Pending check-out" && closingDay.MissingMinutes==0,"EAT closing minute stays pending without a deduction");
        var closingPeriod=new PayrollPeriod{BranchId=creditBranch.Id,From=closingDate,To=closingDate.AddDays(1)};db.Add(closingPeriod);await db.SaveChangesAsync();
        var closingPayroll=new PayrollService(db,calendar,closingAttendance);
        await closingPayroll.CalculateAsync(closingPeriod.Id,true,"closing-fixture");
        check((await db.PayrollLines.SingleAsync(l=>l.PayrollPeriodId==closingPeriod.Id && l.Revision==1)).AttendanceApplied==0,"open and future days cause no premature absence deductions");
        clock.Now=new DateTimeOffset(2026,11,25,12,31,0,TimeSpan.Zero);
        var services=new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddScoped<HrDbContext>(_=>new HrDbContext(options));
        services.AddScoped<CalendarService>();services.AddScoped<AttendanceService>();services.AddSingleton<TimeProvider>(clock);
        await using(var provider=services.BuildServiceProvider())
        {
            var worker=new AttendanceDeadlineWorker(provider.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(),new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),Microsoft.Extensions.Logging.Abstractions.NullLogger<AttendanceDeadlineWorker>.Instance);
            await worker.ClosePendingAsync();
        }
        await db.Entry(closingDay).ReloadAsync();
        check(closingDay.Status=="Absent" && closingDay.MissingMinutes==480 && closingDay.LastOut==null && closingDay.StatusMessage!.Contains("check-out missing"),"background closing marks a missing checkout absent without another device scan");
        await closingPayroll.CalculateAsync(closingPeriod.Id,true,"closing-fixture");
        var closedLine=await db.PayrollLines.SingleAsync(l=>l.PayrollPeriodId==closingPeriod.Id && l.Revision==2);
        check(closedLine.Basic==400 && closedLine.AttendanceApplied==15.38m && closedLine.Net==384.62m,"closed incomplete day deducts 480 minutes while future days remain uncharged");
        db.ZkPunchs.Add(new(){DeviceId=device.Id,Pin=creditEmployee.ZkPin!,PunchTime=closingDate.AddHours(15.5)});await db.SaveChangesAsync();
        await closingAttendance.RebuildAsync(creditEmployee.Id,closingDate);
        check(closingDay.Status=="Present" && closingDay.MissingMinutes==0 && closingDay.WorkedHours==8,"a delayed upload of a valid checkout replaces automatic absence on rebuild");
        // Reported case: 08:59 -> 12:43 is 224 actual minutes, not an automatic eight hours.
        var actualBranch=new Branch{Name="Actual minutes "+key,Code="ACT"+key};db.Add(actualBranch);await db.SaveChangesAsync();
        var actualDept=new Department{Name="Actual minutes",Code="ACT"+key,BranchId=actualBranch.Id};db.Add(actualDept);await db.SaveChangesAsync();
        var actualDate=new DateTime(2026,11,25);
        var actualEmployee=new Employee{EmployeeNumber="ACT"+key,FullName="Actual minute fixture",WorkEmail="actual"+key+"@example.test",HireDate=actualDate,RegistrationComplete=true,ZkPin="ACT"+key};db.Add(actualEmployee);await db.SaveChangesAsync();
        db.EmployeeAssignments.Add(new(){EmployeeId=actualEmployee.Id,BranchId=actualBranch.Id,DepartmentId=actualDept.Id,EffectiveFrom=actualDate});
        db.SalaryVersions.Add(new(){EmployeeId=actualEmployee.Id,Basic=400m,EffectiveFrom=actualDate,IsDraft=false});
        db.WorkRules.Add(new(){BranchId=actualBranch.Id,EffectiveFrom=new(2026,11,1),UsePunchWindows=true,DailyHours=8,BreakMinutes=0,WorkingWeekdays="0,1,2,3,4,6"});
        var actualDay=new AttendanceDay{EmployeeId=actualEmployee.Id,Date=actualDate,FirstIn=actualDate.AddHours(8).AddMinutes(59),LastOut=actualDate.AddHours(12).AddMinutes(43),WorkedHours=8,ExpectedHours=8,Status="Present",CalculationVersion=0};db.Add(actualDay);
        db.ZkPunchs.AddRange(new(){DeviceId=device.Id,Pin=actualEmployee.ZkPin!,PunchTime=actualDay.FirstIn.Value},new(){DeviceId=device.Id,Pin=actualEmployee.ZkPin!,PunchTime=actualDay.LastOut.Value});await db.SaveChangesAsync();
        var actualClock=new ClosingTestClock(new DateTimeOffset(2026,11,25,10,0,0,TimeSpan.Zero)); // 13:00 EAT, before window closes
        var actualServices=new Microsoft.Extensions.DependencyInjection.ServiceCollection();actualServices.AddScoped<HrDbContext>(_=>new HrDbContext(options));actualServices.AddScoped<CalendarService>();actualServices.AddScoped<AttendanceService>();actualServices.AddSingleton<TimeProvider>(actualClock);
        await using(var provider=actualServices.BuildServiceProvider()){
            await new AttendanceDeadlineWorker(provider.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(),new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),Microsoft.Extensions.Logging.Abstractions.NullLogger<AttendanceDeadlineWorker>.Instance).ClosePendingAsync();
        }
        await db.Entry(actualDay).ReloadAsync();
        check(Math.Round(actualDay.WorkedHours*60m)==224 && actualDay.MissingMinutes==256 && actualDay.Status=="Short hours" && actualDay.CalculationVersion==AttendanceService.CalculationVersion,"background rebuild replaces saved 8/8 with 224 worked and 256 missing minutes from unchanged punches");
        check(HrSystemDhg.Web.Pages.Hr.AttendanceModel.HoursAndMinutes(actualDay.WorkedHours)=="3h 44m","attendance displays actual hours and minutes clearly");
        var actualAttendance=new AttendanceService(db,calendar,actualClock);var actualPayroll=new PayrollService(db,calendar,actualAttendance);
        var actualPeriod=new PayrollPeriod{BranchId=actualBranch.Id,From=actualDate,To=actualDate};db.Add(actualPeriod);await db.SaveChangesAsync();await actualPayroll.CalculateAsync(actualPeriod.Id,true,"actual-minute-fixture");
        var actualLine=await db.PayrollLines.SingleAsync(l=>l.PayrollPeriodId==actualPeriod.Id && l.Revision==1);
        check(actualLine.Basic==400m && actualLine.AttendanceApplied==8.21m && actualLine.Net==391.79m,"valid in/out pair before closing deducts 400 / 12480 x 256 = 8.21 without changing basic salary");
        actualDay.IsCorrected=true;actualDay.CorrectionReason="Fixture correction preserves exact punches";actualDay.WorkedHours=8;await db.SaveChangesAsync();await actualAttendance.RebuildAsync(actualEmployee.Id,actualDate);
        check(actualDay.WorkedHours==224m/60m && actualDay.MissingMinutes==256,"corrected attendance also uses actual duration rather than window credit");
        var actualLeave=new LeaveRequest{EmployeeId=actualEmployee.Id,LeaveTypeId=creditLeaveType.Id,From=actualDate,To=actualDate,Status="Approved",Reason="Paid leave fixture"};db.Add(actualLeave);await db.SaveChangesAsync();await actualPayroll.CalculateAsync(actualPeriod.Id,true,"actual-minute-fixture");
        check((await db.PayrollLines.SingleAsync(l=>l.PayrollPeriodId==actualPeriod.Id && l.Revision==2)).AttendanceApplied==0 && actualDay.MissingMinutes==0,"approved paid leave remains free of actual-minute attendance deductions");
        db.Remove(actualLeave);actualDay.IsCorrected=false;
        db.ZkPunchs.AddRange(new(){DeviceId=device.Id,Pin=actualEmployee.ZkPin!,PunchTime=actualDate.AddDays(1).AddHours(8).AddMinutes(59)},new(){DeviceId=device.Id,Pin=actualEmployee.ZkPin!,PunchTime=actualDate.AddDays(1).AddHours(12).AddMinutes(43)});
        var twoDayPeriod=new PayrollPeriod{BranchId=actualBranch.Id,From=actualDate,To=actualDate.AddDays(1)};db.Add(twoDayPeriod);await db.SaveChangesAsync();actualClock.Now=new DateTimeOffset(2026,11,26,13,0,0,TimeSpan.Zero);await actualPayroll.CalculateAsync(twoDayPeriod.Id,true,"actual-minute-fixture");
        var monthlyLine=await db.PayrollLines.SingleAsync(l=>l.PayrollPeriodId==twoDayPeriod.Id);
        check(monthlyLine.AttendanceApplied==16.41m && monthlyLine.Net==383.59m,"monthly total uses 512 missing minutes at basic /12480 with rounding only carried across daily entries");
        check(!(await db.WorkRules.Where(r=>r.BranchId==null && r.EffectiveFrom==creditDate).OrderByDescending(r=>r.Id).FirstAsync()).CreditFullDayForValidPunches,"migration appends actual-minute rules while retaining earlier full-day-credit history");
        var friday = date.AddDays(4);
        await attendance.RebuildAsync(employee.Id, friday);
        var weekend = await db.AttendanceDays.SingleAsync(d => d.EmployeeId == employee.Id && d.Date == friday);
        check(weekend.Status == "Weekend" && weekend.MissingMinutes == 0, "Friday has no absence or missing-minute charge");
        db.ZkPunchs.Add(new() { DeviceId = device.Id, Pin = key, PunchTime = date.AddDays(1).AddHours(15) });
        await db.SaveChangesAsync(); await attendance.RebuildAsync(employee.Id, date.AddDays(1));
        check((await db.AttendanceDays.SingleAsync(d => d.EmployeeId == employee.Id && d.Date == date.AddDays(1))).Status == "Absent", "checkout-only attendance becomes absent after its deadline without inventing a check-in");
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
        var navigationHttp = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        navigationHttp.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role,"Admin"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name,"navigation-fixture")],"test"));
        var navigation = new HrSystemDhg.Web.Pages.Management.PayrollModel(db,new PayrollService(db,calendar,attendance))
        {
            PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext { HttpContext=navigationHttp },
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(navigationHttp,new NavigationTempDataProvider()),
            Month = "2026-01"
        };
        var calculatedResult = await navigation.OnPostCalculateAsync(leavePeriod.Id,true);
        check(calculatedResult is Microsoft.AspNetCore.Mvc.RedirectToPageResult { Fragment:"payroll-register" } result &&
            Convert.ToInt32(result.RouteValues!["PeriodId"])==leavePeriod.Id && (string)result.RouteValues["Month"]! == "2026-11" &&
            Convert.ToInt32(result.RouteValues["PayrollBranchId"])==branch.Id,
            "recalculation returns to updated employee payroll with the selected run, correct month and branch");

    }
}

public sealed class ClosingTestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class NavigationTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
{
    public IDictionary<string,object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string,object>();
    public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context,IDictionary<string,object> values) { }
}
