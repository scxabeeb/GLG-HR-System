using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HrSystemDhg.Web.Data;
public class HrDbContextFactory : IDesignTimeDbContextFactory<HrDbContext>
{
    public HrDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? "Server=localhost;Database=hr_system_dhg;User=hr_app;Password=;";
        return new HrDbContext(new DbContextOptionsBuilder<HrDbContext>().UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 0))).Options);
    }
}
