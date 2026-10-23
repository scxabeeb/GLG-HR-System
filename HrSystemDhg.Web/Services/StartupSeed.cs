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
