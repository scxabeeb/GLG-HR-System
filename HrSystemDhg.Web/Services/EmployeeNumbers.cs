using HrSystemDhg.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;

public static class EmployeeNumbers
{
    // The caller's registration transaction holds this lock until employee and login are committed.
    public static async Task<string> AllocateAsync(HrDbContext db)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Employee numbering requires a registration transaction.");
        var counter = (await db.EmployeeNumberSequences.FromSqlRaw("SELECT * FROM `EmployeeNumberSequences` WHERE `Id` = 1 FOR UPDATE").ToListAsync()).Single();
        string number;
        do
        {
            if (counter.NextNumber == int.MaxValue) throw new InvalidOperationException("Employee numbering capacity reached.");
            number = "EMP" + counter.NextNumber++.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
        }
        while (await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.EmployeeNumber == number) ||
               await db.Users.AnyAsync(u => u.NormalizedUserName == number));
        await db.SaveChangesAsync();
        return number;
    }

    public static async Task<string> AllocatePinAsync(HrDbContext db)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Terminal numbering requires a registration transaction.");
        var counter = (await db.EmployeeNumberSequences.FromSqlRaw("SELECT * FROM `EmployeeNumberSequences` WHERE `Id` = 1 FOR UPDATE").ToListAsync()).Single();
        string pin;
        do
        {
            if (counter.NextPin > 999999999) throw new InvalidOperationException("Terminal numbering capacity reached.");
            pin = counter.NextPin++.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        while (await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.ZkPin == pin) ||
               await db.ZkPunchs.AnyAsync(p => p.Pin == pin) ||
               await db.ZkPunchReviews.AnyAsync(p => p.Pin == pin) ||
               await db.ZkDeviceCommands.AnyAsync(c => c.Pin == pin));
        await db.SaveChangesAsync();
        return pin;
    }

}
