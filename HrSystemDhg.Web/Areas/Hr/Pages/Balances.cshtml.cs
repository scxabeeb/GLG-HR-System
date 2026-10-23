using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Hr;
[Authorize(Roles = "HR,Admin")]
public class BalancesModel(HrDbContext db) : PageModel
{
    public List<Employee> Employees = [];
    public List<LeaveType> Types = [];
    public List<LeaveBalance> Balances = [];
    public async Task OnGetAsync()
    {
        Employees = await db.Employees.ToListAsync();
        Types = await db.LeaveTypes.ToListAsync();
        Balances = await db.LeaveBalances.ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync(int employeeId, int typeId, int year, decimal days, string reason)
    {
        if (days < 0 || year < 2000 || year > 2200 || string.IsNullOrWhiteSpace(reason) || !await db.Employees.AnyAsync(x => x.Id == employeeId) || !await db.LeaveTypes.AnyAsync(x => x.Id == typeId))
            return BadRequest();
        var b = await db.LeaveBalances.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveTypeId == typeId && x.Year == year);
        if (b == null)
        {
            b = new()
            {
                EmployeeId = employeeId,
                LeaveTypeId = typeId,
                Year = year
            };
            db.LeaveBalances.Add(b);
        }

        b.Days = days;
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Leave credit {employeeId}/{typeId}/{year}: {days}; {reason}" });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }
}
