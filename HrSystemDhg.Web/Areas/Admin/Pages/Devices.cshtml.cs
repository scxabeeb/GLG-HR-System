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
    [BindProperty(SupportsGet = true)] public string? Connection { get; set; }
    public static string ConnectionStatus(ZkDevice d) => d.PushSerial == null ? "Not paired" :
        d.LastPushSeenUtc >= DateTime.UtcNow.AddMinutes(-5) ? "Online" : d.LastPushSeenUtc == null ? "Not connected" : "Offline";
    public IEnumerable<ZkDevice> ListedDevices => Items.Where(d => Connection switch {
        "online" => ConnectionStatus(d) == "Online", "offline" => ConnectionStatus(d) is "Offline" or "Not connected",
        "unpaired" => d.PushSerial == null, _ => true }).OrderBy(d => d.Name);
    public List<Branch> Branches = [];
    public List<ZkDevice> Items = [];
    public List<Employee> Employees = [];
    public List<ZkDeviceCommand> Commands = [];

    public async Task OnGetAsync()
    {
        Branches = await db.Branchs.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
        Items = await db.ZkDevices.ToListAsync();
        Employees = await db.Employees.Where(e => e.ZkPin != null && e.ZkPin != "").OrderBy(e => e.FullName).ToListAsync();
        Commands = await db.ZkDeviceCommands.OrderByDescending(c => c.Id).Take(50).ToListAsync();
    }

    public async Task<IActionResult> OnPostEditAsync(int id, string? name, string? endpoint, int? branchId, string? location)
    {
        var device = await db.ZkDevices.FindAsync(id); if (device == null) return NotFound();
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(name) || name.Length > 200 || location?.Length > 250 ||
            !branchId.HasValue || !await db.Branchs.AnyAsync(b => b.Id == branchId))
        { TempData["DeviceNotice"] = "Enter a name, valid branch and location up to 250 characters."; return RedirectToPage(null, null, $"device-{id}"); }
        device.Name = name.Trim(); device.Endpoint = string.IsNullOrWhiteSpace(endpoint) ? "Manual import" : endpoint.Trim();
        device.BranchId = branchId; device.DepartmentId = null; device.Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Edited device {id}: {device.Name}, branch {branchId}" });
        await db.SaveChangesAsync(); TempData["DeviceNotice"] = "Device updated.";
        return RedirectToPage(null, null, $"device-{id}");
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var device = await db.ZkDevices.FindAsync(id); if (device == null) return NotFound();
        if (await db.ZkPunchs.AnyAsync(p => p.DeviceId == id) || await db.ZkPunchReviews.AnyAsync(p => p.DeviceId == id) || await db.ZkDeviceCommands.AnyAsync(c => c.DeviceId == id))
        { TempData["DeviceNotice"] = "This device has attendance or command history and cannot be deleted. Disable automatic sync to disconnect it while retaining its records."; return RedirectToPage(null, null, $"device-{id}"); }
        if (device.PushSerial != null)
        { TempData["DeviceNotice"] = "Disable automatic sync before deleting this paired device."; return RedirectToPage(null, null, $"device-{id}"); }
        db.ZkDevices.Remove(device);
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Deleted unused device {id}: {device.Name}" });
        await db.SaveChangesAsync(); TempData["DeviceNotice"] = "Device deleted.";
        return RedirectToPage(null, null, "devices-list");
    }

    public async Task<IActionResult> OnPostLocationAsync(int id, int? branchId, string? location)
    {
        var device = await db.ZkDevices.FindAsync(id);
        if (device == null) return NotFound();
        if (!ModelState.IsValid || location?.Trim().Length > 250 ||
            (!branchId.HasValue || !await db.Branchs.AnyAsync(b => b.Id == branchId)))
        {
            TempData["DeviceNotice"] = "Choose a valid branch and a location of up to 250 characters.";
            return RedirectToPage();
        }
        device.BranchId = branchId;
        device.DepartmentId = null;
        device.Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Device {id}: branch {branchId}, location {device.Location}" });
        await db.SaveChangesAsync();
        TempData["DeviceNotice"] = "Device branch and location saved.";
        return RedirectToPage(null, null, $"device-{id}");
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
    public async Task<IActionResult> OnPostAsync(string name, string? endpoint, int? branchId, string? location)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(name) || name.Length > 200 || location?.Length > 250 ||
            !branchId.HasValue || !await db.Branchs.AnyAsync(b => b.Id == branchId))
        {
            const string message = "Enter a device name, choose a valid branch, and use a location up to 250 characters.";
            ModelState.AddModelError("", message); TempData["DeviceNotice"] = message;
            return RedirectToPage(null, null, "devices-register");
        }
        db.ZkDevices.Add(new() { Name = name.Trim(), Endpoint = string.IsNullOrWhiteSpace(endpoint) ? "Manual import" : endpoint.Trim(), BranchId = branchId, Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim() });
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Added attendance device {name} for branch {branchId}" });
        await db.SaveChangesAsync();
        TempData["DeviceNotice"] = "Attendance device registered for the selected branch.";
        return RedirectToPage(null, null, "devices-list");
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
            device.OfficeConnectorKeyHash = null; device.LastOfficeConnectorSeenUtc = null;
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
        TempData["DeviceNotice"] = disable ? "Automatic sync disabled." : "Device pairing saved. For the online system, download the office connector script and configuration below, then set the terminal’s ADMS server to the office computer’s IP and connector port.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConnectorAsync(int id, bool revoke = false)
    {
        var device = await db.ZkDevices.FindAsync(id); if (device == null) return NotFound();
        if (revoke)
        {
            device.OfficeConnectorKeyHash = null; device.LastOfficeConnectorSeenUtc = null;
            db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Revoked office connector for device {id}" });
            await db.SaveChangesAsync(); TempData["DeviceNotice"] = "Office connector revoked.";
            return RedirectToPage(null, null, $"device-{id}");
        }
        if (device.PushSerial == null || device.PushAddress == null) { TempData["DeviceNotice"] = "Save ADMS pairing first."; return RedirectToPage(null, null, $"device-{id}"); }
        var key = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        device.OfficeConnectorKeyHash = OperationsPolicy.HashTrackerKey(key); device.LastOfficeConnectorSeenUtc = null;
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Issued office connector configuration for device {id}" });
        await db.SaveChangesAsync();
        Response.Headers.CacheControl = "no-store";
        var origin = $"{(env.IsDevelopment() ? Request.Scheme : "https")}://{Request.Host}";
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { server = origin, listen = "192.168.50.1", port = 5190,
            terminals = new[] { new { id, serial = device.PushSerial, address = device.PushAddress, key } } }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        return File(bytes, "application/json", $"attendance-connector-{id}.json");
    }
    public IActionResult OnGetConnectorScript() => PhysicalFile(Path.Combine(env.ContentRootPath, "Connectors", "attendance_connector.py"), "application/octet-stream", "attendance_connector.py");

    private const string CsvColumns = "Name,Endpoint,BranchId,Location";

    public async Task<IActionResult> OnGetSampleAsync()
    {
        var branch = await db.Branchs.AsNoTracking().OrderBy(b => b.Id).Select(b => (int?)b.Id).FirstOrDefaultAsync();
        return RegistrationCsv.Download("attendance-devices-sample", CsvColumns, [["Example terminal", "Manual import", branch, "Main office"]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var records = await db.ZkDevices.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("attendance-devices", CsvColumns, records.Select(x => new object?[] { x.Name, x.Endpoint, x.BranchId, x.Location }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload)
    {
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            var name = row.Get<string>("Name");
            if (await db.ZkDevices.AnyAsync(x => x.Name == name))
                throw new FormatException("A device with this name already exists.");
            return await OnPostAsync(name, row.Optional("Endpoint"), row.Get<int?>("BranchId"), row.Optional("Location"));
        });
    }
}
