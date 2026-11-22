using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;
public static class StartupSeed
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[]
        {
            "Admin",
            "Management",
            "HR",
            "General Secretary",
            "Chairman",
            "Employee"
        }

        )
            if (!await roles.RoleExistsAsync(role))
            {
                var result = await roles.CreateAsync(new(role));
                if (!result.Succeeded)
                    throw new InvalidOperationException(string.Join(";", result.Errors.Select(x => x.Description)));
            }

        if (!await db.Settings.AnyAsync())
            db.Settings.Add(new());
        await db.SaveChangesAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = config["Bootstrap:Email"];
        var password = config["Bootstrap:Password"];
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
        {
            var existing = await users.FindByEmailAsync(email);
            if (existing?.PasswordHash != null)
            {
                try { Convert.FromBase64String(existing.PasswordHash); }
                catch (FormatException)
                {
                    existing.PasswordHash = users.PasswordHasher.HashPassword(existing, password);
                    existing.MustChangePassword = true;
                    existing.AccessFailedCount = 0;
                    existing.LockoutEnd = null;
                    existing.SecurityStamp = Guid.NewGuid().ToString();
                    var repaired = await users.UpdateAsync(existing);
                    if (!repaired.Succeeded) throw new InvalidOperationException("Could not repair the bootstrap account.");
                    db.AuditLogs.Add(new() { Actor = existing.Id, Action = "Repaired malformed bootstrap password hash; password change required" });
                    await db.SaveChangesAsync();
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password) && await users.FindByEmailAsync(email) == null)
        {
            var u = new ApplicationUser
            {
                UserName = email,
                Email = email,
                MustChangePassword = true
            };
            var result = await users.CreateAsync(u, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(";", result.Errors.Select(x => x.Description)));
            await users.AddToRoleAsync(u, "Admin");
        }
    }
}
