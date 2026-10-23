using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Reports;
[Authorize(Roles = "HR,Management,Admin")]
public class IndexModel(HrDbContext db,LeaveService leaves) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Report { get; set; } = "Employees";

    [BindProperty(SupportsGet = true)]
    public DateTime From { get; set; } = new(HrSystemDhg.Web.Services.BusinessClock.Today.Year, HrSystemDhg.Web.Services.BusinessClock.Today.Month, 1);

    [BindProperty(SupportsGet = true)]
    public DateTime To { get; set; } = HrSystemDhg.Web.Services.BusinessClock.Today;

    [BindProperty(SupportsGet = true)]
    public int? BranchId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? DepartmentId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SubDepartmentId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PeriodId { get; set; }

    public List<Branch> Branches = [];
    public List<Department> Departments = [];
    public List<SubDepartment> SubDepartments = [];
    public List<PayrollPeriod> Periods = [];
    public List<string[]> Rows = [];
    public async Task<IActionResult> OnGetAsync(string? format)
    {
        Branches = await db.Branchs.ToListAsync();
        Departments = await db.Departments.ToListAsync();
        SubDepartments = await db.SubDepartments.ToListAsync();
        Periods = await db.PayrollPeriods.OrderByDescending(x => x.From).ToListAsync();
        if (From > To || (To - From).TotalDays > 3660)
            return BadRequest();
        var employees = await db.Employees.ToDictionaryAsync(x => x.Id);
        var org = await db.EmployeeAssignments.Where(x => x.EffectiveFrom <= To && (x.EffectiveTo == null || x.EffectiveTo >= To)).ToListAsync();
        var ids = employees.Keys.Where(id => org.Any(a => a.EmployeeId == id && (BranchId == null || a.BranchId == BranchId) && (DepartmentId == null || a.DepartmentId == DepartmentId) && (SubDepartmentId == null || a.SubDepartmentId == SubDepartmentId))).ToList();
        string Name(int id) => employees.GetValueOrDefault(id)?.FullName ?? "";
        if (Report is "Employees" or "Hires")
        {
            Rows.Add(["Number", "Name", "Email", "Hire date", "Termination date", "Status"]);
            foreach (var e in employees.Values.Where(x => ids.Contains(x.Id) && (Report != "Hires" || x.HireDate >= From && x.HireDate <= To || x.TerminationDate >= From && x.TerminationDate <= To)))
                Rows.Add([e.EmployeeNumber, e.FullName, e.WorkEmail, e.HireDate.ToString("yyyy-MM-dd"), e.TerminationDate?.ToString("yyyy-MM-dd") ?? "", e.Status]);
        }
        else if (Report == "Attendance")
        {
            Rows.Add(["Employee", "Date", "In", "Out", "Hours", "Late minutes", "Status"]);
            foreach (var a in await db.AttendanceDays.Where(x => ids.Contains(x.EmployeeId) && x.Date >= From && x.Date <= To).OrderBy(x => x.Date).ToListAsync())
                Rows.Add([Name(a.EmployeeId), a.Date.ToString("yyyy-MM-dd"), a.FirstIn?.ToString("HH:mm") ?? "", a.LastOut?.ToString("HH:mm") ?? "", a.WorkedHours.ToString("F2"), a.LateMinutes.ToString(), a.Status]);
        }
        else if (Report == "Leave")
        {
            Rows.Add(["Employee", "From", "To", "Status", "Reason"]);
            foreach (var r in await db.LeaveRequests.Where(x => ids.Contains(x.EmployeeId) && x.From <= To && x.To >= From).ToListAsync())
                Rows.Add([Name(r.EmployeeId), r.From.ToString("yyyy-MM-dd"), r.To.ToString("yyyy-MM-dd"), r.Status, r.Reason]);
        }
        else if(Report=="Balances")
        {
            Rows.Add(["Employee","Leave type","Year","Remaining days"]);
            foreach(var e in employees.Values.Where(x=>ids.Contains(x.Id)))
                foreach(var type in await db.LeaveTypes.ToListAsync())
                    Rows.Add([e.FullName,type.Name,From.Year.ToString(),type.EnforceBalance?(await leaves.RemainingAsync(e.Id,type.Id,From.Year)).ToString("F2"):"Unlimited"]);
        }
        else if (Report is "Payroll" or "Deductions" or "Bank")
        {
            if (PeriodId == null)
            {
                ModelState.AddModelError("", "Select a payroll period.");
                return Page();
            }

            var period = await db.PayrollPeriods.SingleOrDefaultAsync(x => x.Id == PeriodId);
            if (period == null)
                return NotFound();
            var lines = await db.PayrollLines.Where(x => x.PayrollPeriodId == PeriodId && x.Revision == period.Revision && (BranchId == null || x.BranchId == BranchId) && (DepartmentId == null || x.DepartmentId == DepartmentId) && (SubDepartmentId == null || x.SubDepartmentId == SubDepartmentId)).ToListAsync();
            if (Report == "Bank")
            {
                var settings = await db.Settings.FirstOrDefaultAsync() ?? new();
                if (!User.IsInRole("Admin") && !User.IsInRole("HR") && !settings.ManagementBankAccess)
                    return Forbid();
                Rows.Add(["Employee", "Bank", "Account name", "Account number", "Currency", "Net"]);
                foreach (var l in lines)
                {
                    var s = await db.SalaryVersions.Where(x => x.EmployeeId == l.EmployeeId && !x.IsDraft && x.EffectiveFrom <= period.To).OrderByDescending(x => x.EffectiveFrom).FirstAsync();
                    Rows.Add([Name(l.EmployeeId), s.BankName ?? "", s.AccountName, s.AccountNumber ?? "", l.Currency, l.Net.ToString("F2")]);
                }
            }
            else if (Report == "Deductions")
            {
                Rows.Add(["Employee", "Date", "Type", "Calculated", "Applied", "Waived", "Reason"]);
                var lineIds = lines.Select(x => x.Id).ToList();
                foreach (var d in await db.PayrollDeductions.Where(x => lineIds.Contains(x.PayrollLineId)).ToListAsync())
                {
                    var l = lines.Single(x => x.Id == d.PayrollLineId);
                    Rows.Add([Name(l.EmployeeId), d.Date.ToString("yyyy-MM-dd"), d.Type, d.Calculated.ToString("F2"), d.Applied.ToString("F2"), (d.Calculated - d.Applied).ToString("F2"), d.Reason ?? ""]);
                }
            }
            else
            {
                Rows.Add(["Employee", "Currency", "Basic", "Allowances", "Overtime", "Gross", "Attendance", "Unpaid leave", "Net"]);
                foreach (var l in lines)
                    Rows.Add([Name(l.EmployeeId), l.Currency, l.Basic.ToString("F2"), l.AllowanceTotal.ToString("F2"), l.Overtime.ToString("F2"), l.Gross.ToString("F2"), l.AttendanceApplied.ToString("F2"), l.UnpaidLeave.ToString("F2"), l.Net.ToString("F2")]);
            }
        }
        else
            return BadRequest();
        if (format != null)
        {
            db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = "Exported " + Report + " report" });
            await db.SaveChangesAsync();
            if (format == "csv")
                return File(ExportService.Csv(Rows), "text/csv", Report + ".csv");
            if (format == "excel")
                return File(ExportService.Excel(Rows), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Report + ".xlsx");
            return BadRequest();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostPublishAsync(int id, [FromServices] PayrollService payroll)
    {
        try
        {
            await payroll.PublishAsync(id, User.Identity!.Name!);
            TempData["Message"] = "Payslips published.";
        }
        catch (InvalidOperationException e)
        {
            TempData["Message"] = e.Message;
        }

        return RedirectToPage(new { report = "Payroll", periodId = id });
    }
}
