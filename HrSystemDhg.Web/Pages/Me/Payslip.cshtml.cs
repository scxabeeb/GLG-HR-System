using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Me;
[Authorize(Roles = "Employee,HR,Management,Admin")]
public class PayslipModel(HrDbContext db) : PageModel
{
    public PayrollLine Line = null !;
    public PayrollPeriod Period = null !;
    public Employee Employee = null !;
    public List<PayrollDeduction> Deductions = [];
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var line = await db.PayrollLines.SingleOrDefaultAsync(x => x.Id == id);
        if (line == null)
            return NotFound();
        if (!User.IsInRole("Admin") && !User.IsInRole("HR") && !User.IsInRole("Management"))
        {
            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!await db.Users.AnyAsync(x => x.Id == uid && x.EmployeeId == line.EmployeeId) || !await db.Payslips.AnyAsync(x => x.PayrollLineId == id && x.IsPublished))
                return NotFound();
        }

        Deductions = await db.PayrollDeductions.AsNoTracking().Where(d=>d.PayrollLineId==id).OrderBy(d=>d.Date).ThenBy(d=>d.Id).ToListAsync();
        Line = line;
        Period = await db.PayrollPeriods.SingleAsync(x => x.Id == line.PayrollPeriodId);
        Employee = await db.Employees.SingleAsync(x => x.Id == line.EmployeeId);
        return Page();
    }
}
