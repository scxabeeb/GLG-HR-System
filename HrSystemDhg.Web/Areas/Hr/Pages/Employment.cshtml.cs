using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Hr;
[Authorize(Roles = "HR,Admin")]
public class EmploymentModel(HrDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int EmployeeId { get; set; }

    [BindProperty]
    public string Status { get; set; } = "Active";

    [BindProperty]
    public DateTime? Termination { get; set; }

    [BindProperty]
    public int? ShiftId { get; set; }

    [BindProperty]
    public string Grade { get; set; } = "";

    [BindProperty]
    public DateTime? ContractEnd { get; set; }

    [BindProperty]
    public string Reason { get; set; } = "";

    public Employee Employee = null !;
    public List<Shift> Shifts = [];
    public async Task OnGetAsync()
    {
        Employee = await db.Employees.SingleAsync(x => x.Id == EmployeeId);
        Status = Employee.Status;
        Termination = Employee.TerminationDate;
        Shifts = await db.Shifts.Where(x => x.IsActive).ToListAsync();
        var a = await db.EmployeeAssignments.Where(x => x.EmployeeId == EmployeeId && x.EffectiveTo == null).SingleAsync();
        ShiftId = a.ShiftId;
        Grade = a.Grade;
        ContractEnd = a.ContractEnd;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var e = await db.Employees.SingleAsync(x => x.Id == EmployeeId);
        if (!new[]
        {
            "Active",
            "Probation",
            "Suspended",
            "Terminated"
        }.Contains(Status) || Status == "Terminated" && Termination == null || Termination < e.HireDate || string.IsNullOrWhiteSpace(Reason) || ShiftId != null && !await db.Shifts.AnyAsync(x => x.Id == ShiftId && x.IsActive))
            return BadRequest();
        e.Status = Status;
        e.TerminationDate = Termination;
        var a = await db.EmployeeAssignments.Where(x => x.EmployeeId == EmployeeId && x.EffectiveTo == null).SingleAsync();
        a.ShiftId = ShiftId;
        a.Grade = Grade;
        a.ContractEnd = ContractEnd;
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Employment updated {EmployeeId}: {Reason}" });
        await db.SaveChangesAsync();
        return RedirectToPage("Details", new { employeeId = EmployeeId });
    }
}
