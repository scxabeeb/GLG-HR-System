using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Me;
[Authorize(Roles = "Employee,HR,Management,Admin")]
public class ProfileModel(HrDbContext db, LeaveService leaves, DocumentService documents) : PageModel
{
    public Employee? Employee;
    public int? PhotoId;
    public SalaryVersion? Salary;
    public List<LeaveType> Types = [];
    public List<LeaveRequest> Leave = [];
    public List<AttendanceDay> Attendance = [];
    public List<ZkPunch> Punches = [];
    public List<PayrollLine> Pay = [];
    public List<CorrectionRequest> Corrections = [];
    public Dictionary<int, string> Periods = [];
    public Dictionary<string, decimal> Balances = [];
    [BindProperty(SupportsGet = true)]
    public string Month { get; set; } = HrSystemDhg.Web.Services.BusinessClock.Today.ToString("yyyy-MM");

    [BindProperty]
    public int LeaveTypeId { get; set; }

    [BindProperty]
    public DateTime From { get; set; } = HrSystemDhg.Web.Services.BusinessClock.Today;

    [BindProperty]
    public DateTime To { get; set; } = HrSystemDhg.Web.Services.BusinessClock.Today;

    [BindProperty]
    public TimeSpan? Start { get; set; }

    [BindProperty]
    public TimeSpan? End { get; set; }

    [BindProperty]
    public string Reason { get; set; } = "";

    [BindProperty]
    public IFormFile? Upload { get; set; }

    private async Task<int?> OwnId()
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return await db.Users.Where(x => x.Id == id).Select(x => x.EmployeeId).SingleOrDefaultAsync();
    }

    public async Task OnGetAsync()
    {
        var id = await OwnId();
        Employee = await db.Employees.SingleOrDefaultAsync(x => x.Id == id);
        if (Employee == null)
            return;
        PhotoId = await db.EmployeeDocuments.Where(x => x.EmployeeId == id && x.Type == "Photo").OrderByDescending(x => x.Id).Select(x => (int? )x.Id).FirstOrDefaultAsync();
        Types = await db.LeaveTypes.ToListAsync();
        Leave = await db.LeaveRequests.Where(x => x.EmployeeId == id).OrderByDescending(x => x.Id).ToListAsync();
        Salary = await db.SalaryVersions.Where(x => x.EmployeeId == id && !x.IsDraft && x.EffectiveFrom <= HrSystemDhg.Web.Services.BusinessClock.Today && (x.EffectiveTo == null || x.EffectiveTo >= HrSystemDhg.Web.Services.BusinessClock.Today)).OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync();
        foreach (var t in Types)
            Balances[t.Name] = await leaves.RemainingAsync(id!.Value, t.Id, HrSystemDhg.Web.Services.BusinessClock.Today.Year);
        if (!DateTime.TryParse(Month + "-01", out var month))
            month = new(HrSystemDhg.Web.Services.BusinessClock.Today.Year, HrSystemDhg.Web.Services.BusinessClock.Today.Month, 1);
        var to = month.AddMonths(1);
        var days = await db.AttendanceDays.Where(x => x.EmployeeId == id && x.Date >= month && x.Date < to).ToDictionaryAsync(x => x.Date);
        for (var d = month; d < to; d = d.AddDays(1))
        {
            if (days.TryGetValue(d, out var day))
                Attendance.Add(day);
            else
            {
                var approved = Leave.Any(l => l.Status == "Approved" && l.From <= d && l.To >= d && l.StartTime == null);
                Attendance.Add(new() { Date = d, Status = approved ? "On leave" : "No summary" });
            }
        }

        Punches = await db.ZkPunchs.Where(x => x.Pin == Employee.ZkPin && x.PunchTime >= month && x.PunchTime < to).OrderBy(x => x.PunchTime).ToListAsync();
        Pay = await db.PayrollLines.Where(x => x.EmployeeId == id && db.Payslips.Any(s => s.PayrollLineId == x.Id && s.IsPublished)).ToListAsync();
        Periods = await db.PayrollPeriods.ToDictionaryAsync(x => x.Id, x => x.From.ToString("MMM yyyy"));
        Corrections = await db.CorrectionRequests.Where(x => x.EmployeeId == id).ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var id = await OwnId();
        if (id == null)
            return Forbid();
        try
        {
            int? doc = null;
            if (Upload != null)
                doc = await documents.StoreAsync(id.Value, Upload, "Leave supporting document", null, User.Identity!.Name!);
            await leaves.SubmitAsync(new() { EmployeeId = id.Value, LeaveTypeId = LeaveTypeId, From = From, To = To, StartTime = Start, EndTime = End, Reason = Reason, DocumentId = doc }, User.Identity!.Name!);
            return RedirectToPage();
        }
        catch (InvalidOperationException e)
        {
            ModelState.AddModelError("", e.Message);
            await OnGetAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        var own = await OwnId();
        var r = await db.LeaveRequests.SingleOrDefaultAsync(x => x.Id == id && x.EmployeeId == own && x.Status == "Pending");
        if (r == null)
            return NotFound();
        r.Status = "Cancelled";
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCorrectionAsync(DateTime? date, string details)
    {
        var id = await OwnId();
        if (id == null)
            return Forbid();
        if (string.IsNullOrWhiteSpace(details) || details.Length > 4000)
            return BadRequest();
        db.CorrectionRequests.Add(new() { EmployeeId = id.Value, AttendanceDate = date, Details = details });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }
}
