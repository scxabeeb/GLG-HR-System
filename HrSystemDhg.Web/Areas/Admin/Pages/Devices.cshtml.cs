using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class DevicesModel(HrDbContext db, IWebHostEnvironment env) : PageModel
{
    public List<Department> Departments = [];
    public List<ZkDevice> Items = [];
    public List<Employee> Employees = [];
    public List<ZkDeviceCommand> Commands = [];

    public async Task OnGetAsync()
    {
        Departments = await db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
        Items = await db.ZkDevices.ToListAsync();
        Employees = await db.Employees.Where(e => e.ZkPin != null && e.ZkPin != "").OrderBy(e => e.FullName).ToListAsync();
        Commands = await db.ZkDeviceCommands.OrderByDescending(c => c.Id).Take(50).ToListAsync();
    }

    public async Task<IActionResult> OnPostLocationAsync(int id, int? departmentId, string? location)
    {
        var device = await db.ZkDevices.FindAsync(id);
        if (device == null) return NotFound();
        if (!ModelState.IsValid || location?.Trim().Length > 250 ||
            (departmentId.HasValue && !await db.Departments.AnyAsync(d => d.Id == departmentId)))
        {
            TempData["DeviceNotice"] = "Choose a valid department and a location of up to 250 characters.";
            return RedirectToPage();
        }
        device.DepartmentId = departmentId;
        device.Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Device {id}: department {departmentId}, location {device.Location}" });
        await db.SaveChangesAsync();
        TempData["DeviceNotice"] = "Device department and location saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEmployeeAsync(int id, int employeeId, string operation, int fingerSlot)
    {
        var device = await db.ZkDevices.FindAsync(id);
        if (device?.PushSerial == null || device.PushAddress == null) return NotFound();
        if (!ModelState.IsValid || operation is not ("sync" or "enroll") || fingerSlot is < 0 or > 9) return BadRequest();

        var employee = await db.Employees.FindAsync(employeeId);
        if (employee == null || !ZkEmployeeCommands.ValidPin(employee.ZkPin))
        {
            TempData["DeviceNotice"] = "Choose an employee with a numeric ZKTeco PIN of up to nine digits, without leading zeros.";
            return RedirectToPage();
        }

        var now = DateTime.UtcNow;
        if (await db.ZkDeviceCommands.AnyAsync(c => c.DeviceId == id && c.Serial == device.PushSerial &&
            c.Address == device.PushAddress && c.Result == null && c.ExpiresUtc > now))
        {
            TempData["DeviceNotice"] = "Wait for the terminal to finish its current command.";
            return RedirectToPage();
        }

        db.ZkDeviceCommands.Add(ZkEmployeeCommands.Create(device, employee, operation == "enroll" ? fingerSlot : null));
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Queued terminal {operation} for employee {employeeId} on device {id}" });
        await db.SaveChangesAsync();
        TempData["DeviceNotice"] = "Command queued. Refresh this page to check the terminal’s response.";
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostAsync(string name, string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest();
        db.ZkDevices.Add(new() { Name = name, Endpoint = endpoint ?? "Manual import" });
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = "Added attendance device " + name });
        await db.SaveChangesAsync();
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostPushAsync(int id, string? serial, string? address, bool disable)
    {
        var device = await db.ZkDevices.FindAsync(id);
        if (device == null) return NotFound();
        var sn = disable ? null : ZkAttendancePush.Serial(serial?.Trim());
        var ip = !disable && System.Net.IPAddress.TryParse(address, out var parsed)
            ? ZkAttendancePush.LocalAddress(parsed, env.IsDevelopment()) : null;
        if (!disable && (sn == null || ip == null))
        {
            TempData["DeviceNotice"] = "Enter the terminal’s serial number and a private office IPv4 address.";
            return RedirectToPage();
        }
        if (sn != null && await db.ZkDevices.AnyAsync(d => d.Id != id && d.PushSerial == sn))
        {
            TempData["DeviceNotice"] = "That serial number is already paired with another device.";
            return RedirectToPage();
        }
        if (device.PushSerial != sn || device.PushAddress != ip)
        {
            var pending = await db.ZkDeviceCommands.Where(c => c.DeviceId == id && c.Result == null && c.ExpiresUtc > DateTime.UtcNow).ToListAsync();
            foreach (var command in pending) command.ExpiresUtc = DateTime.UtcNow;
            device.PushStamp = null;
            device.LastPushSeenUtc = null;
            device.LastSuccess = null;
            device.LastError = null;
        }
        device.PushSerial = sn;
        device.PushAddress = ip;
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Device {id}: automatic attendance {(disable ? "disabled" : "configured")}" });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException { Number: 1062 })
        {
            TempData["DeviceNotice"] = "That serial number is already paired with another device.";
            return RedirectToPage();
        }
        TempData["DeviceNotice"] = disable ? "Automatic sync disabled." : "Device pairing saved. Configure the terminal’s ADMS server address and port.";
        return RedirectToPage();
    }

    private const string CsvColumns = "Name,Endpoint";

    public IActionResult OnGetSample()
    {
        return RegistrationCsv.Download("attendance-devices-sample", CsvColumns, [["Example terminal", "Manual import"]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var records = await db.ZkDevices.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("attendance-devices", CsvColumns, records.Select(x => new object?[] { x.Name, x.Endpoint }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload)
    {
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            var name = row.Get<string>("Name");
            if (await db.ZkDevices.AnyAsync(x => x.Name == name))
                throw new FormatException("A device with this name already exists.");
            return await OnPostAsync(name, row.Optional("Endpoint"));
        });
    }
}
