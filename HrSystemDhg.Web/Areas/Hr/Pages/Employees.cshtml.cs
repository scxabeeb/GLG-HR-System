using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Hr;

[Authorize(Roles = "HR,Admin")]
public class EmployeesModel(HrDbContext db) : PageModel
{
    public sealed record AssignmentRow(int EmployeeId, string Branch, string Department, string JobTitle);

    [BindProperty(SupportsGet = true)] public int? ExpandedEmployeeId { get; set; }

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Department { get; set; }
    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string RowsPerPage {get;set;}="20";
    [BindProperty(SupportsGet = true)] public int PageNumber {get;set;}=1;
    public ListPager Paging {get;private set;}=new("20",1,0);
    public string PageLink(int number)=>Url.Page("./Employees",new{Search,Department,StatusFilter,RowsPerPage,PageNumber=number})??"";
    public string[] DepartmentOptions = [];
    public string[] StatusOptions = [];
    public int TotalEmployees { get; set; }
    public List<Employee> Items = [];
    public List<ZkDevice> Terminals = [];
    public Dictionary<int, AssignmentRow> Assignments = [];
    public ILookup<int, EmployeeDocument> Documents = Array.Empty<EmployeeDocument>().ToLookup(d => d.EmployeeId);
    public ILookup<string, ZkDeviceCommand> Commands = Array.Empty<ZkDeviceCommand>().ToLookup(c => c.Pin);

    public async Task OnGetAsync()
    {
        var filtered = db.Employees.AsNoTracking();
        Terminals = await db.ZkDevices.AsNoTracking().Where(d => d.PushSerial != null && d.PushAddress != null)
            .OrderBy(d => d.Name).ToListAsync();

        var today = BusinessClock.Today;
        var assignments = await (from assignment in db.EmployeeAssignments.AsNoTracking()
                                 where assignment.EffectiveFrom <= today && (assignment.EffectiveTo == null || assignment.EffectiveTo >= today)
                                 join branch in db.Branchs on assignment.BranchId equals branch.Id
                                 join department in db.Departments on assignment.DepartmentId equals department.Id
                                 orderby assignment.EffectiveFrom descending, assignment.Id descending
                                 select new AssignmentRow(assignment.EmployeeId, branch.Name, department.Name, assignment.JobTitle)).ToListAsync();
        Assignments = assignments.GroupBy(a => a.EmployeeId).ToDictionary(group => group.Key, group => group.First());

        TotalEmployees = await filtered.CountAsync();
        DepartmentOptions = Assignments.Values.Select(a => a.Department).Distinct().Order().ToArray();
        StatusOptions = await filtered.Select(e => e.RegistrationComplete ? e.Status : "Onboarding").Distinct().OrderBy(s=>s).ToArrayAsync();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term=Search.Trim().ToLowerInvariant();
            var jobMatches=Assignments.Values.Where(a=>a.JobTitle.Contains(Search.Trim(),StringComparison.OrdinalIgnoreCase)).Select(a=>a.EmployeeId).ToArray();
            filtered=filtered.Where(e=>e.FullName.ToLower().Contains(term) || e.EmployeeNumber.ToLower().Contains(term) ||
                e.WorkEmail.ToLower().Contains(term) || e.Phone.ToLower().Contains(term) || jobMatches.Contains(e.Id));
        }
        if (!string.IsNullOrWhiteSpace(Department))
        {
            var matches=Assignments.Values.Where(a=>a.Department==Department).Select(a=>a.EmployeeId).ToArray();
            filtered=filtered.Where(e=>matches.Contains(e.Id));
        }
        if (!string.IsNullOrWhiteSpace(StatusFilter))
            filtered=filtered.Where(e=>(e.RegistrationComplete ? e.Status : "Onboarding")==StatusFilter);
        Paging=new(RowsPerPage,PageNumber,await filtered.CountAsync());RowsPerPage=Paging.RowsPerPage;PageNumber=Paging.PageNumber;
        var page=filtered.OrderBy(e=>e.FullName).ThenBy(e=>e.Id).Skip(Paging.Skip).AsQueryable();
        if(Paging.Size.HasValue)page=page.Take(Paging.Size.Value);
        Items=await page.ToListAsync();
        var employeeIds=Items.Select(e=>e.Id).ToArray();
        var pins=Items.Where(e=>e.ZkPin!=null).Select(e=>e.ZkPin!).ToArray();
        Documents=(await db.EmployeeDocuments.AsNoTracking().Where(d=>employeeIds.Contains(d.EmployeeId))
            .OrderBy(d=>d.Type).ThenBy(d=>d.OriginalName).ToListAsync()).ToLookup(d=>d.EmployeeId);

        var commands = await db.ZkDeviceCommands.AsNoTracking()
            .Where(c => pins.Contains(c.Pin) && db.Employees.Any(e => e.ZkPin == c.Pin) &&
                db.ZkDevices.Any(d => d.Id == c.DeviceId && d.PushSerial == c.Serial && d.PushAddress == c.Address))
            .OrderByDescending(c => c.Id).ToListAsync();
        Commands = commands.DistinctBy(c => (c.Pin, c.DeviceId, IsEnrollment: c.FingerSlot != null)).ToLookup(c => c.Pin);
    }

    public static string Status(ZkDeviceCommand command) => command.Result switch
    {
        0 => "Terminal acknowledged success",
        int code => $"Terminal returned {code}",
        _ when command.ExpiresUtc <= DateTime.UtcNow => command.SentUtc == null ? "Expired before sending" : "No terminal acknowledgement",
        _ => command.SentUtc == null ? "Queued" : "Sent · awaiting terminal"
    };

    public async Task<IActionResult> OnPostTerminalAsync(int employeeId, int terminalId, string operation, int fingerSlot)
    {
        if (!ModelState.IsValid || operation is not ("sync" or "enroll") || fingerSlot is < 0 or > 9) return BadRequest();
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Id == employeeId);
        var terminal = await db.ZkDevices.SingleOrDefaultAsync(d => d.Id == terminalId && d.PushSerial != null && d.PushAddress != null);
        if (employee == null || terminal == null) return NotFound();
        if (!ZkEmployeeCommands.ValidPin(employee.ZkPin))
        {
            TempData["TerminalNotice"] = "The employee needs a numeric terminal User ID of up to nine digits.";
            return RedirectToPage("./Employees", null, new { expandedEmployeeId = employeeId, Search, Department, StatusFilter, RowsPerPage, PageNumber }, $"employee-{employeeId}");
        }

        var now = DateTime.UtcNow;
        var slot = operation == "enroll" ? (int?)fingerSlot : null;
        if (await db.ZkDeviceCommands.AnyAsync(c => c.DeviceId == terminalId && c.Serial == terminal.PushSerial &&
            c.Address == terminal.PushAddress && c.Pin == employee.ZkPin && c.FingerSlot == slot &&
            c.Result == null && c.ExpiresUtc > now))
        {
            TempData["TerminalNotice"] = "That command is already pending.";
            return RedirectToPage("./Employees", null, new { expandedEmployeeId = employeeId, Search, Department, StatusFilter, RowsPerPage, PageNumber }, $"employee-{employeeId}");
        }

        db.ZkDeviceCommands.Add(ZkEmployeeCommands.Create(terminal, employee, slot));
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Queued terminal {operation} for employee {employeeId} on device {terminalId}" });
        await db.SaveChangesAsync();
        TempData["TerminalNotice"] = "Command queued. Refresh the employee directory to check the terminal’s response.";
        return RedirectToPage("./Employees", null, new { expandedEmployeeId = employeeId, Search, Department, StatusFilter, RowsPerPage, PageNumber }, $"employee-{employeeId}");
    }
}
