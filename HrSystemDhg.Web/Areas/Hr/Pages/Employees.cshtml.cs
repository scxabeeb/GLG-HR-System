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

    public List<Employee> Items = [];
    public List<ZkDevice> Terminals = [];
    public Dictionary<int, AssignmentRow> Assignments = [];
    public ILookup<int, EmployeeDocument> Documents = Array.Empty<EmployeeDocument>().ToLookup(d => d.EmployeeId);
    public ILookup<string, ZkDeviceCommand> Commands = Array.Empty<ZkDeviceCommand>().ToLookup(c => c.Pin);

    public async Task OnGetAsync()
    {
        Items = await db.Employees.AsNoTracking().OrderBy(e => e.FullName).ToListAsync();
        Terminals = await db.ZkDevices.AsNoTracking().Where(d => d.PushSerial != null && d.PushAddress != null)
            .OrderBy(d => d.Name).ToListAsync();
        Documents = (await db.EmployeeDocuments.AsNoTracking().OrderBy(d => d.Type).ThenBy(d => d.OriginalName)
            .ToListAsync()).ToLookup(d => d.EmployeeId);

        var today = BusinessClock.Today;
        var assignments = await (from assignment in db.EmployeeAssignments.AsNoTracking()
                                 where assignment.EffectiveFrom <= today && (assignment.EffectiveTo == null || assignment.EffectiveTo >= today)
                                 join branch in db.Branchs on assignment.BranchId equals branch.Id
                                 join department in db.Departments on assignment.DepartmentId equals department.Id
                                 orderby assignment.EffectiveFrom descending, assignment.Id descending
                                 select new AssignmentRow(assignment.EmployeeId, branch.Name, department.Name, assignment.JobTitle)).ToListAsync();
        Assignments = assignments.GroupBy(a => a.EmployeeId).ToDictionary(group => group.Key, group => group.First());

        var commands = await db.ZkDeviceCommands.AsNoTracking()
            .Where(c => db.Employees.Any(e => e.ZkPin == c.Pin) &&
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
            return RedirectToPage("./Employees", null, new { expandedEmployeeId = employeeId }, $"employee-{employeeId}");
        }

        var now = DateTime.UtcNow;
        var slot = operation == "enroll" ? (int?)fingerSlot : null;
        if (await db.ZkDeviceCommands.AnyAsync(c => c.DeviceId == terminalId && c.Serial == terminal.PushSerial &&
            c.Address == terminal.PushAddress && c.Pin == employee.ZkPin && c.FingerSlot == slot &&
            c.Result == null && c.ExpiresUtc > now))
        {
            TempData["TerminalNotice"] = "That command is already pending.";
            return RedirectToPage("./Employees", null, new { expandedEmployeeId = employeeId }, $"employee-{employeeId}");
        }

        db.ZkDeviceCommands.Add(ZkEmployeeCommands.Create(terminal, employee, slot));
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Queued terminal {operation} for employee {employeeId} on device {terminalId}" });
        await db.SaveChangesAsync();
        TempData["TerminalNotice"] = "Command queued. Refresh the employee directory to check the terminal’s response.";
        return RedirectToPage("./Employees", null, new { expandedEmployeeId = employeeId }, $"employee-{employeeId}");
    }
}
