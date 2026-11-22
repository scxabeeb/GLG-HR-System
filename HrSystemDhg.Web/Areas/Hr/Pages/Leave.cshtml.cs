using System.ComponentModel.DataAnnotations;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Hr;

[Authorize(Roles = "HR,Management,Admin,General Secretary")]
public class LeaveModel(HrDbContext db, LeaveService leaves) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Year { get; set; } = BusinessClock.Today.Year;
    [BindProperty(SupportsGet = true)] public string ViewMode { get; set; } = "";
    [BindProperty(SupportsGet = true)] public int? EmployeeId { get; set; }
    [BindProperty(SupportsGet = true)] public int? TypeId { get; set; }
    [BindProperty(SupportsGet = true)] public bool Record { get; set; }
    [BindProperty] public RequestInput? NewRequest { get; set; }
    public int? OwnEmployeeId { get; private set; }

    public class RequestInput
    {
        [Range(1, int.MaxValue)] public int EmployeeId { get; set; }
        [Range(1, int.MaxValue)] public int TypeId { get; set; }
        [Required] public DateTime? From { get; set; }
        [Required] public DateTime? To { get; set; }
        public TimeSpan? Start { get; set; }
        public TimeSpan? End { get; set; }
        [Required, StringLength(2000)] public string Reason { get; set; } = "";
        public IFormFile? Document { get; set; }
    }

    public int AwaitingHr { get; private set; }
    public int AwaitingDecision { get; private set; }
    public bool CanSubmit => User.IsInRole("HR") || User.IsInRole("Admin");
    public bool CanDecide => User.IsInRole("Admin") || User.IsInRole("General Secretary");
    public List<LeaveRequest> Items { get; private set; } = [];
    public Dictionary<int, Employee> Employees { get; private set; } = [];
    public Dictionary<int, LeaveType> Types { get; private set; } = [];
    public List<BalanceRow> Balances { get; } = [];
    public record BalanceRow(Employee Employee, LeaveType Type, decimal? Used, decimal? Remaining, string Source);

    public async Task OnGetAsync()
    {
        if (Year is < 1900 or > 9998) Year = BusinessClock.Today.Year;
        var yearStart = new DateTime(Year, 1, 1);
        var until = yearStart.AddYears(1);
        ViewMode = ViewMode is "hr" or "approval" or "history" or "balances" ? ViewMode :
            CanSubmit ? "hr" : CanDecide ? "approval" : "history";
        var requests = db.LeaveRequests.AsNoTracking().Where(r => r.From < until && r.To >= yearStart &&
            (EmployeeId == null || r.EmployeeId == EmployeeId) && (TypeId == null || r.LeaveTypeId == TypeId));
        var all = await requests.OrderByDescending(r => r.Id).ToListAsync();
        AwaitingHr = all.Count(r => r.Status is "Pending" or "Manager approved");
        AwaitingDecision = all.Count(r => r.Status == LeaveService.Submitted);
        Items = ViewMode switch
        {
            "hr" => all.Where(r => r.Status is "Pending" or "Manager approved").ToList(),
            "approval" => all.Where(r => r.Status == LeaveService.Submitted).ToList(),
            _ => all
        };
        Employees = await db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id);
        Types = await db.LeaveTypes.AsNoTracking().ToDictionaryAsync(t => t.Id);
        if (CanSubmit)
        {
            OwnEmployeeId = await db.Users.Where(u => u.Id == db.ActorId).Select(u => u.EmployeeId).SingleOrDefaultAsync();
            NewRequest ??= new() { EmployeeId = OwnEmployeeId ?? 0, From = BusinessClock.Today, To = BusinessClock.Today };
        }
        if (ViewMode != "balances") return;
        var credits = await db.LeaveBalances.AsNoTracking().Where(b => b.Year == Year).ToListAsync();
        var creditKeys = credits.Select(b => (b.EmployeeId, b.LeaveTypeId)).ToHashSet();
        var requestKeys = all.Select(r => (r.EmployeeId, r.LeaveTypeId));
        var eligible = from employee in Employees.Values
                       where employee.HireDate < until && (employee.TerminationDate == null || employee.TerminationDate >= yearStart)
                       from type in Types.Values
                       where (string.IsNullOrWhiteSpace(type.GenderRestriction) || type.GenderRestriction == employee.Gender)
                          && (string.IsNullOrWhiteSpace(type.EmploymentRestriction) || type.EmploymentRestriction == employee.EmploymentType)
                       select (EmployeeId: employee.Id, LeaveTypeId: type.Id);
        var keys = eligible.Concat(requestKeys).Concat(creditKeys).Distinct()
            .Where(k => Employees.ContainsKey(k.EmployeeId) && Types.ContainsKey(k.LeaveTypeId) &&
                (EmployeeId == null || k.EmployeeId == EmployeeId) && (TypeId == null || k.LeaveTypeId == TypeId))
            .OrderBy(k => Employees[k.EmployeeId].FullName).ThenBy(k => Types[k.LeaveTypeId].Name);
        foreach (var key in keys)
        {
            var employee = Employees[key.EmployeeId];
            var type = Types[key.LeaveTypeId];
            var source = !type.EnforceBalance ? "Unlimited" : creditKeys.Contains(key) ? "Individual credit" : "Leave type default";
            try
            {
                var balance = await leaves.BalanceAsync(employee.Id, type.Id, Year);
                Balances.Add(new(employee, type, balance.Used, type.EnforceBalance ? balance.Remaining : null, source));
            }
            catch (InvalidOperationException)
            {
                Balances.Add(new(employee, type, null, null, source));
            }
        }
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        await OnGetAsync();
        Response.Headers.CacheControl = "no-store";
        if (ViewMode == "balances")
            return RegistrationCsv.Download("leave-balances", "EmployeeNumber,Employee,LeaveType,Year,Source,Used,Remaining",
                Balances.Select(b => new object?[] { b.Employee.EmployeeNumber, b.Employee.FullName, b.Type.Name, Year,
                    b.Source, b.Used, b.Remaining }));
        return RegistrationCsv.Download("leave-requests", "EmployeeNumber,Employee,LeaveType,From,To,Status,DecidedBy",
            Items.Select(x => new object?[] { Employees.GetValueOrDefault(x.EmployeeId)?.EmployeeNumber,
                Employees.GetValueOrDefault(x.EmployeeId)?.FullName, Types.GetValueOrDefault(x.LeaveTypeId)?.Name,
                x.From, x.To, x.Status, x.DecidedBy }));
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!CanSubmit) return Forbid();
        var input = NewRequest;
        if (input == null || !await db.Employees.AnyAsync(e => e.Id == input.EmployeeId))
            ModelState.AddModelError("NewRequest.EmployeeId", "Select an employee you can manage.");
        if (input == null || !await db.LeaveTypes.AnyAsync(t => t.Id == input.TypeId))
            ModelState.AddModelError("NewRequest.TypeId", "Select a leave type.");
        if (!ModelState.IsValid || input?.From == null || input.To == null)
        {
            await OnGetAsync();
            return Page();
        }

        return await Apply(async () =>
        {
            await leaves.SubmitAsync(new()
            {
                EmployeeId = input.EmployeeId, LeaveTypeId = input.TypeId, From = input.From.Value.Date,
                To = input.To.Value.Date, StartTime = input.Start, EndTime = input.End, Reason = input.Reason.Trim()
            }, User.Identity!.Name!, input.Document);
            TempData["LeaveNotice"] = "Request recorded. It is waiting for submission for final approval.";
            Year = input.From.Value.Year;
            ViewMode = "hr";
            EmployeeId = input.EmployeeId;
            TypeId = null;
        });
    }

    public Task<IActionResult> OnPostSubmitAsync(int id) => CanSubmit
        ? Apply(() => leaves.ForwardAsync(id, User.Identity!.Name!))
        : Task.FromResult<IActionResult>(Forbid());

    public Task<IActionResult> OnPostAsync(int id, string decision, string? reason) => CanDecide
        ? Apply(() => leaves.DecideAsync(id, decision, reason ?? "", User.Identity!.Name!, null, true))
        : Task.FromResult<IActionResult>(Forbid());

    private async Task<IActionResult> Apply(Func<Task> action)
    {
        try
        {
            await action();
            return RedirectToPage(new { Year, ViewMode, EmployeeId, TypeId });
        }
        catch (InvalidOperationException ex)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", ex.Message);
            await OnGetAsync();
            return Page();
        }
    }
}
