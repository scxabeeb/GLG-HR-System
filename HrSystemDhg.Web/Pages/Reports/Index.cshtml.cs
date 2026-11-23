using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Reports;
[Authorize(Roles = "HR,Management,Admin")]
public class IndexModel(HrDbContext db,LeaveService leaves,PortalBranding branding) : PageModel
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

    [BindProperty(SupportsGet = true)]
    public int? EmployeeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)] public int? SavedViewId { get; set; }
    [BindProperty(SupportsGet = true)] public bool FiltersApplied { get; set; }
    public string? SavedViewName { get; private set; }
    private SavedReportView? savedView;
    public bool IsPayrollReport => Report is "Payroll" or "Deductions" or "Bank";
    public bool UsesDates => Report is "Hires" or "Attendance" or "Salary" or "Leave" or "Balances";
    public bool CanViewSalary => User.IsInRole("HR") || User.IsInRole("Admin");
    public bool CanPublish => User.IsInRole("Management") || User.IsInRole("Admin");
    public List<Employee> Employees { get; private set; } = [];
    public List<string> Statuses { get; private set; } = [];

    public List<Branch> Branches = [];
    public List<Department> Departments = [];
    public List<SubDepartment> SubDepartments = [];
    public List<PayrollPeriod> Periods = [];
    public List<string[]> Rows = [];
    public async Task<IActionResult> OnGetAsync(string? format)
    {
        if (SavedViewId.HasValue)
        {
            savedView = await db.SavedReportViews.AsNoTracking().SingleOrDefaultAsync(v => v.Id == SavedViewId && v.IsActive);
            if (savedView == null || !ReportDefinitions.Sources.ContainsKey(savedView.Source)) return NotFound();
            Report = savedView.Source; SavedViewName = savedView.Name;
            if (!FiltersApplied)
            {
                var today = BusinessClock.Today;
                var month = new DateTime(today.Year, today.Month, 1);
                From = savedView.DateRange == "ThisYear" ? new DateTime(today.Year, 1, 1) : savedView.DateRange == "LastMonth" ? month.AddMonths(-1) : month;
                To = savedView.DateRange == "LastMonth" ? month.AddDays(-1) : today;
                BranchId = savedView.BranchId; DepartmentId = savedView.DepartmentId; Status = savedView.Status;
            }
        }
        Branches = await db.Branchs.ToListAsync();
        Departments = await db.Departments.ToListAsync();
        SubDepartments = await db.SubDepartments.ToListAsync();
        if (Report == "Salary" && !CanViewSalary)
            return Forbid();
        if (IsPayrollReport)
            Periods = await db.PayrollPeriods.OrderByDescending(x => x.From).ToListAsync();
        Employees = await db.Employees.AsNoTracking().OrderBy(x => x.FullName).ThenBy(x => x.EmployeeNumber).ToListAsync();
        if (UsesDates && (From > To || (To - From).TotalDays > 3660))
            ModelState.AddModelError("", "Choose a valid date range of up to ten years.");
        if (!ModelState.IsValid)
            return format == null ? Page() : BadRequest();

        var employees = Employees.ToDictionary(x => x.Id);
        var eligible = db.Employees.AsNoTracking();
        var organizationDate = UsesDates ? To : BusinessClock.Today;
        var org = db.EmployeeAssignments.AsNoTracking().Where(x => x.EffectiveFrom <= organizationDate
            && (x.EffectiveTo == null || x.EffectiveTo >= organizationDate)
            && (BranchId == null || x.BranchId == BranchId)
            && (DepartmentId == null || x.DepartmentId == DepartmentId)
            && (SubDepartmentId == null || x.SubDepartmentId == SubDepartmentId));
        if (BranchId != null || DepartmentId != null || SubDepartmentId != null)
            eligible = eligible.Where(x => org.Any(a => a.EmployeeId == x.Id));
        var ids = IsPayrollReport ? [] : await eligible.Where(x => EmployeeId == null || x.Id == EmployeeId)
            .Select(x => x.Id).ToListAsync();
        string Name(int id) => employees.GetValueOrDefault(id)?.FullName ?? "";
        if (Report is "Employees" or "Hires")
        {
            var assignments = (await org.Where(x => ids.Contains(x.EmployeeId))
                .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Id).ToListAsync())
                .GroupBy(x => x.EmployeeId).ToDictionary(x => x.Key, x => x.First());
            var branches = Branches.ToDictionary(x => x.Id, x => x.Name);
            var departments = Departments.ToDictionary(x => x.Id, x => x.Name);
            var subDepartments = SubDepartments.ToDictionary(x => x.Id, x => x.Name);
            Rows.Add(["Number", "Name", "Email", "Job title", "Branch", "Department", "Sub-department",
                "Hire date", "Termination date", "Status"]);
            Statuses = Employees.Select(x => x.Status).Distinct().OrderBy(x => x).ToList();
            foreach (var e in Employees.Where(x => ids.Contains(x.Id)
                && (Status == null || x.Status == Status)
                && (Report != "Hires" || x.HireDate >= From && x.HireDate <= To || x.TerminationDate >= From && x.TerminationDate <= To)))
            {
                var assignment = assignments.GetValueOrDefault(e.Id);
                Rows.Add([e.EmployeeNumber, e.FullName, e.WorkEmail, assignment?.JobTitle ?? "",
                    branches.GetValueOrDefault(assignment?.BranchId ?? 0) ?? "",
                    departments.GetValueOrDefault(assignment?.DepartmentId ?? 0) ?? "",
                    subDepartments.GetValueOrDefault(assignment?.SubDepartmentId ?? 0) ?? "",
                    e.HireDate.ToString("yyyy-MM-dd"), e.TerminationDate?.ToString("yyyy-MM-dd") ?? "", e.Status]);
            }
        }
        else if (Report == "Attendance")
        {
            var days = db.AttendanceDays.AsNoTracking().Where(x => ids.Contains(x.EmployeeId) && x.Date >= From && x.Date <= To);
            Statuses = await days.Select(x => x.Status).Distinct().OrderBy(x => x).ToListAsync();
            Rows.Add(["Employee", "Date", "Time in (EAT)", "Time out (EAT)", "Hours", "Late minutes", "Missing minutes", "Status"]);
            foreach (var a in await days.Where(x => Status == null || x.Status == Status)
                .OrderByDescending(x => x.Date).ThenBy(x => x.EmployeeId).ToListAsync())
                Rows.Add([Name(a.EmployeeId), a.Date.ToString("yyyy-MM-dd"), a.FirstIn?.ToString("hh:mm tt") ?? "", a.LastOut?.ToString("hh:mm tt") ?? "", a.WorkedHours.ToString("F2"), a.LateMinutes.ToString(), a.MissingMinutes.ToString(), a.Status]);
        }
        else if (Report == "Salary")
        {
            Statuses = ["Published", "Draft"];
            Rows.Add(["Employee number", "Employee", "Effective from", "Effective to", "Status",
                "Currency", "Basic", "Housing", "Transport", "Other allowances", "Total"]);
            var salaries = await db.SalaryVersions.AsNoTracking()
                .Where(x => ids.Contains(x.EmployeeId) && x.EffectiveFrom <= To && (x.EffectiveTo == null || x.EffectiveTo >= From)
                    && (Status == null || (x.IsDraft ? "Draft" : "Published") == Status))
                .OrderByDescending(x => x.EffectiveFrom).ThenBy(x => x.EmployeeId).ToListAsync();
            foreach (var s in salaries)
                Rows.Add([employees[s.EmployeeId].EmployeeNumber, Name(s.EmployeeId),
                    s.EffectiveFrom.ToString("yyyy-MM-dd"), s.EffectiveTo?.ToString("yyyy-MM-dd") ?? "",
                    s.IsDraft ? "Draft" : "Published", s.Currency, s.Basic.ToString("F2"), s.Housing.ToString("F2"),
                    s.Transport.ToString("F2"), s.Allowances.ToString("F2"),
                    (s.Basic + s.Housing + s.Transport + s.Allowances).ToString("F2")]);
        }
        else if (Report == "Leave")
        {
            Rows.Add(["Employee", "From", "To", "Status", "Reason"]);
            foreach (var r in await db.LeaveRequests.Where(x => ids.Contains(x.EmployeeId) && x.From <= To && x.To >= From && (Status == null || x.Status == Status)).ToListAsync())
                Rows.Add([Name(r.EmployeeId), r.From.ToString("yyyy-MM-dd"), r.To.ToString("yyyy-MM-dd"), r.Status, r.Reason]);
        }
        else if(Report=="Balances")
        {
            Rows.Add(["Employee","Leave type","Year","Remaining days"]);
            foreach(var e in employees.Values.Where(x=>ids.Contains(x.Id)))
                foreach(var type in await db.LeaveTypes.ToListAsync())
                    Rows.Add([e.FullName,type.Name,From.Year.ToString(),type.EnforceBalance?(await leaves.RemainingAsync(e.Id,type.Id,From.Year)).ToString("F2"):"Unlimited"]);
        }
        else if (IsPayrollReport)
        {
            if (PeriodId == null)
            {
                return format == null ? Page() : BadRequest("Select a payroll period.");
            }

            var period = await db.PayrollPeriods.SingleOrDefaultAsync(x => x.Id == PeriodId);
            if (period == null)
                return NotFound();
            var lines = await db.PayrollLines.Where(x => x.PayrollPeriodId == PeriodId && x.Revision == period.Revision
                && (EmployeeId == null || x.EmployeeId == EmployeeId)
                && (BranchId == null || x.BranchId == BranchId) && (DepartmentId == null || x.DepartmentId == DepartmentId)
                && (SubDepartmentId == null || x.SubDepartmentId == SubDepartmentId))
                .OrderBy(x => x.EmployeeId).ToListAsync();
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
        if (savedView != null && Rows.Count > 0)
        {
            var columns = ReportDefinitions.ColumnIndexes(savedView.Columns).Where(i => i >= 0 && i < Rows[0].Length).ToArray();
            if (columns.Length == 0) return BadRequest("This report has no valid columns. Ask an administrator to edit its definition.");
            Rows = Rows.Select(row => columns.Select(i => row[i]).ToArray()).ToList();
        }
        if (format != null)
        {
            if (format is not ("pdf" or "excel" or "csv")) return BadRequest();
            Response.Headers.CacheControl = "no-store";
            db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = "Exported " + Report + " report" });
            await db.SaveChangesAsync();
            if (format == "pdf")
            {
                var brand = await branding.GetAsync();
                return File(ReportPdf.Create(SavedViewName ?? Report + " report", Rows, brand.Organization, brand.Name), "application/pdf", Report + ".pdf");
            }
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
        if (!CanPublish)
            return Forbid();

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
