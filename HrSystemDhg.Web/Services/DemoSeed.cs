using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;
public static class DemoSeed
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration config, IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
            throw new InvalidOperationException("Demo seeding is available only in Development.");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        if (!db.Database.GetDbConnection().Database.Contains("preview", StringComparison.OrdinalIgnoreCase) && !db.Database.GetDbConnection().Database.Contains("demo", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use a separate database containing preview or demo in its name.");
        var password = config["Demo:Password"] ?? throw new InvalidOperationException("Set Demo__Password before seeding.");
        if (await db.Employees.AnyAsync(x => x.EmployeeNumber == "DEMO-001"))
            return;
        var branch = new Branch
        {
            Name = "Garowe Central",
            Code = "GWC",
            City = "Garowe",
            Timezone = "Africa/Mogadishu"
        };
        db.Branchs.Add(branch);
        await db.SaveChangesAsync();
        var departments = new[]
        {
            new Department
            {
                Name = "Administration",
                Code = "ADM",
                BranchId = branch.Id
            },
            new Department
            {
                Name = "Finance",
                Code = "FIN",
                BranchId = branch.Id
            },
            new Department
            {
                Name = "Public Services",
                Code = "PS",
                BranchId = branch.Id
            }
        };
        db.Departments.AddRange(departments);
        await db.SaveChangesAsync();
        if (!await db.WorkRules.AnyAsync())
            db.WorkRules.Add(new() { EffectiveFrom = new(2026, 1, 1), DailyHours = 8, StartHour = 8, BreakMinutes = 60 });
        if (!await db.DeductionRules.AnyAsync())
            db.DeductionRules.Add(new() { EffectiveFrom = new(2026, 1, 1), LatePerMinute = .25m, AbsenceMultiplier = 1 });
        var annual = new LeaveType
        {
            Name = "Annual",
            AnnualDays = 30
        };
        db.LeaveTypes.AddRange(annual, new() { Name = "Sick", AnnualDays = 15 }, new() { Name = "Unpaid", IsPaid = false, EnforceBalance = false });
        var device = new ZkDevice
        {
            Name = "Central office · manual import",
            Endpoint = "Manual CSV / punch entry"
        };
        db.ZkDevices.Add(device);
        await db.SaveChangesAsync();
        var names = new[]
        {
            "Amina Yusuf",
            "Mohamed Ali",
            "Sahra Ahmed",
            "Abdirahman Hassan",
            "Fadumo Omar",
            "Hassan Mohamed",
            "Maryan Abdi",
            "Yusuf Said",
            "Hodan Ismail",
            "Ahmed Farah",
            "Nimco Osman",
            "Abdi Nur"
        };
        var employees = new List<Employee>();
        for (int i = 0; i < names.Length; i++)
        {
            var e = new Employee
            {
                EmployeeNumber = $"DEMO-{i + 1:000}",
                FullName = names[i],
                WorkEmail = $"person{i + 1}@demo.garowe.local",
                HireDate = new(2026, 1, 1),
                ZkPin = $"D{i + 1:000}",
                Gender = i % 2 == 0 ? "Female" : "Male",
                Nationality = "Somali",
                RegistrationComplete = true
            };
            db.Employees.Add(e);
            await db.SaveChangesAsync();
            employees.Add(e);
            db.EmployeeAssignments.Add(new() { EmployeeId = e.Id, BranchId = branch.Id, DepartmentId = departments[i % 3].Id, EffectiveFrom = e.HireDate, JobTitle = i == 0 ? "HR Officer" : i == 1 ? "Operations Manager" : "Officer" });
            db.SalaryVersions.Add(new() { EmployeeId = e.Id, Basic = 1200 + i * 100, Housing = 150, Transport = 50, Currency = "USD", EffectiveFrom = e.HireDate, OvertimeEligible = true, BankName = "Demo bank", AccountName = e.FullName, AccountNumber = $"DEMO-{i + 1:000000}" });
        }

        await db.SaveChangesAsync();
        foreach (var e in employees.Skip(2))
            e.ManagerId = employees[1].Id;
        await db.SaveChangesAsync();
        for (var date = BusinessClock.Today.AddDays(-15); date <= BusinessClock.Today; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            foreach (var e in employees)
            {
                db.ZkPunchs.AddRange(new() { DeviceId = device.Id, Pin = e.ZkPin!, PunchTime = date.AddHours(8).AddMinutes(e.Id % 3 == 0 ? 20 : 0) }, new() { DeviceId = device.Id, Pin = e.ZkPin!, PunchTime = date.AddHours(17) });
            }
        }

        await db.SaveChangesAsync();
        var attendance = scope.ServiceProvider.GetRequiredService<AttendanceService>();
        foreach (var e in employees)
            for (var date = BusinessClock.Today.AddDays(-15); date <= BusinessClock.Today; date = date.AddDays(1))
                await attendance.RebuildAsync(e.Id, date);
        db.LeaveRequests.Add(new() { EmployeeId = employees[2].Id, LeaveTypeId = annual.Id, From = BusinessClock.Today.AddDays(7), To = BusinessClock.Today.AddDays(8), Reason = "Demo annual leave request" });
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var accounts = new[]
        {
            ("admin@demo.garowe.local", "Admin", (int? )null),
            ("hr@demo.garowe.local", "HR", (int? )employees[0].Id),
            ("management@demo.garowe.local", "Management", (int? )employees[1].Id),
            ("employee@demo.garowe.local", "Employee", (int? )employees[2].Id)
        };
        foreach (var(email, role, employeeId)in accounts)
        {
            var u = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmployeeId = employeeId,
                MustChangePassword = false
            };
            var result = await users.CreateAsync(u, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(";", result.Errors.Select(x => x.Description)));
            await users.AddToRoleAsync(u, role);
        }

        db.AuditLogs.Add(new() { Actor = "Demo setup", Action = "Created synthetic preview data and four demo accounts" });
        await db.SaveChangesAsync();
    }
}
