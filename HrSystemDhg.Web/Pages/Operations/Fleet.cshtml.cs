using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace HrSystemDhg.Web.Pages.Operations;
[Authorize(Roles = "Admin,Management")]
public class FleetModel(HrDbContext db) : PageModel
{
    public VehicleInput Input { get; set; } = new();
    public ServiceInput Service { get; set; } = new();
    public List<FleetVehicle> Vehicles { get; private set; } = [];
    public Dictionary<int,string> Branches { get; private set; } = [];
    public Dictionary<int,string> Drivers { get; private set; } = [];
    public FleetVehicle? Selected { get; private set; }
    public List<FleetPosition> Positions { get; private set; } = [];
    public List<FleetServiceEntry> Services { get; private set; } = [];
    public class VehicleInput
    {
        public int Id { get; set; }
        [Required, StringLength(32)] public string Plate { get; set; } = "";
        [Required, StringLength(120)] public string Name { get; set; } = "";
        [Range(1, int.MaxValue)] public int BranchId { get; set; }
        public int? DriverId { get; set; }
        [Range(0, 9999999)] public int OdometerKm { get; set; }
        public DateTime? RegistrationExpires { get; set; }
        public DateTime? InsuranceExpires { get; set; }
        public DateTime? InspectionDue { get; set; }
        [EnumDataType(typeof(VehicleState))] public VehicleState Status { get; set; }
    }
    public class ServiceInput
    {
        [Required] public DateTime? Date { get; set; }
        [Required, StringLength(500)] public string? Description { get; set; }
    }
    private IQueryable<Branch> ActiveBranches()
    {
        var ids = db.AllowedBranchIds;
        return db.Branchs.Where(b => b.IsActive && (!db.Scoped || ids.Contains(b.Id)));
    }

    private async Task Load(FleetVehicle? selected)
    {
        Response.Headers.CacheControl = "no-store";
        Vehicles = await OperationsPolicy.Vehicles(db).AsNoTracking().OrderBy(v => v.Plate).ToListAsync();
        Branches = await ActiveBranches().OrderBy(b => b.Name).ToDictionaryAsync(b => b.Id, b => b.Name);
        Drivers = await db.Employees.AsNoTracking().OrderBy(e => e.FullName).ToDictionaryAsync(e => e.Id, e => e.FullName);
        Selected = selected;
        if (Selected != null)
        {
            Positions = await db.FleetPositions.AsNoTracking().Where(p => p.VehicleId == Selected.Id)
                .OrderByDescending(p => p.RecordedUtc).Take(50).ToListAsync();
            Services = await db.FleetServiceEntries.AsNoTracking().Where(s => s.VehicleId == Selected.Id)
                .OrderBy(s => s.Completed).ThenByDescending(s => s.ServiceDate).ToListAsync();
        }
    }

    public async Task<IActionResult> OnGetAsync(int? vehicle)
    {
        var item = vehicle.HasValue ? await OperationsPolicy.Vehicles(db).AsNoTracking().SingleOrDefaultAsync(v => v.Id == vehicle) : null;
        if (vehicle.HasValue && item == null) return NotFound();
        if (item != null) Input = new() { Id = item.Id, Plate = item.Plate, Name = item.Name, BranchId = item.BranchId,
            DriverId = item.DriverId, OdometerKm = item.OdometerKm, Status = item.Status, RegistrationExpires = item.RegistrationExpires, InsuranceExpires = item.InsuranceExpires, InspectionDue = item.InspectionDue };
        await Load(item);
        return Page();
    }

    public async Task<IActionResult> OnGetLatestAsync(int id)
    {
        Response.Headers.CacheControl = "no-store";
        if (!await OperationsPolicy.Vehicles(db).AnyAsync(v => v.Id == id)) return NotFound();
        var fix = await db.FleetPositions.AsNoTracking().Where(p => p.VehicleId == id)
            .OrderByDescending(p => p.RecordedUtc).FirstOrDefaultAsync();
        return new JsonResult(new { position = fix == null ? null : new
        {
            latitude = fix.Latitude, longitude = fix.Longitude, speedKph = fix.SpeedKph,
            localTime = OperationsPolicy.Local(fix.RecordedUtc).ToString("dd MMM yyyy, HH:mm:ss") + " EAT",
            stale = fix.RecordedUtc < DateTime.UtcNow.AddMinutes(-15)
        } });
    }

    public async Task<IActionResult> OnPostSaveAsync([Bind(Prefix = "Input")] VehicleInput input)
    {
        Input = input;
        var vehicle = Input.Id == 0 ? new FleetVehicle() : await OperationsPolicy.Vehicles(db).SingleOrDefaultAsync(v => v.Id == Input.Id);
        if (vehicle == null) return NotFound();
        var plate = Input.Plate?.Trim().ToUpperInvariant();
        if (!await ActiveBranches().AnyAsync(b => b.Id == Input.BranchId))
            ModelState.AddModelError("Input.BranchId", "Choose an active branch you can manage.");
        var today = BusinessClock.Today;
        if (Input.DriverId.HasValue && !await db.EmployeeAssignments.AnyAsync(a =>
            a.EmployeeId == Input.DriverId && a.BranchId == Input.BranchId && a.EffectiveFrom <= today &&
            (a.EffectiveTo == null || a.EffectiveTo >= today)))
            ModelState.AddModelError("Input.DriverId", "Choose a driver with a current assignment in the vehicle’s branch.");
        if (await db.FleetVehicles.AnyAsync(v => v.Plate == plate && v.Id != Input.Id))
            ModelState.AddModelError("Input.Plate", "A vehicle with this registration already exists.");
        if (new[] { Input.RegistrationExpires, Input.InsuranceExpires, Input.InspectionDue }.Any(d => d?.Year < 1900))
            ModelState.AddModelError("", "Enter valid renewal dates.");
        if (Input.Id != 0 && Input.OdometerKm < vehicle.OdometerKm)
            ModelState.AddModelError("Input.OdometerKm", "Mileage cannot go backwards.");
        if (Input.Id != 0 && (Input.BranchId != vehicle.BranchId || Input.OdometerKm != vehicle.OdometerKm || Input.Status != vehicle.Status))
        {
            var active = await db.FleetTrips.AnyAsync(t => t.VehicleId == Input.Id && t.Status == FleetTripState.InProgress);
            var approved = await db.FleetTrips.AnyAsync(t => t.VehicleId == Input.Id && t.Status == FleetTripState.Approved);
            if (active || approved && (Input.BranchId != vehicle.BranchId || Input.Status is VehicleState.Retired or VehicleState.Maintenance))
                ModelState.AddModelError("", "Resolve existing trip bookings before changing this vehicle’s branch, availability or mileage.");
        }
        if (!ModelState.IsValid) { await Load(Input.Id == 0 ? null : vehicle); return Page(); }

        vehicle.Plate = plate!;
        vehicle.Name = Input.Name.Trim();
        vehicle.BranchId = Input.BranchId;
        vehicle.DriverId = Input.DriverId;
        vehicle.OdometerKm = Input.OdometerKm;
        vehicle.Status = Input.Status;
        vehicle.RegistrationExpires = Input.RegistrationExpires?.Date;
        vehicle.InsuranceExpires = Input.InsuranceExpires?.Date;
        vehicle.InspectionDue = Input.InspectionDue?.Date;
        if (vehicle.Status == VehicleState.Retired) vehicle.TrackerKeyHash = null;
        if (Input.Id == 0) db.FleetVehicles.Add(vehicle);
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = Input.Id == 0 ? "Registered vehicle" : $"Updated vehicle {vehicle.Id}" });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("Input.Plate", "A vehicle with this registration already exists.");
            await Load(Input.Id == 0 ? null : vehicle);
            return Page();
        }
        TempData["OperationsNotice"] = "Vehicle saved.";
        return RedirectToPage(new { vehicle = vehicle.Id });
    }

    public async Task<IActionResult> OnPostTrackerAsync(int id, bool revoke)
    {
        var vehicle = await OperationsPolicy.Vehicles(db).SingleOrDefaultAsync(v => v.Id == id);
        if (vehicle == null) return NotFound();
        if (!revoke && vehicle.Status == VehicleState.Retired) return BadRequest();
        var key = revoke ? null : Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        vehicle.TrackerKeyHash = key == null ? null : OperationsPolicy.HashTrackerKey(key);
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Vehicle {id}: tracker key {(revoke ? "revoked" : "rotated")}" });
        await db.SaveChangesAsync();
        if (key != null) { TempData["FleetTrackerKey"] = key; TempData["FleetTrackerVehicle"] = id; }
        TempData["OperationsNotice"] = revoke ? "Tracker disconnected." : "Tracker key issued. Copy it now; it will not be shown again.";
        return RedirectToPage(new { vehicle = id });
    }

    public async Task<IActionResult> OnPostServiceAsync(int id, [Bind(Prefix = "Service")] ServiceInput service)
    {
        Service = service;
        var vehicle = await OperationsPolicy.Vehicles(db).AsNoTracking().SingleOrDefaultAsync(v => v.Id == id);
        if (vehicle == null) return NotFound();
        if (Service.Date?.Year < 1900) ModelState.AddModelError("Service.Date", "Select a valid service date.");
        if (!ModelState.IsValid)
        {
            Input = new() { Id = vehicle.Id, Plate = vehicle.Plate, Name = vehicle.Name, BranchId = vehicle.BranchId,
                DriverId = vehicle.DriverId, OdometerKm = vehicle.OdometerKm, Status = vehicle.Status, RegistrationExpires = vehicle.RegistrationExpires, InsuranceExpires = vehicle.InsuranceExpires, InspectionDue = vehicle.InspectionDue };
            await Load(vehicle);
            return Page();
        }
        db.FleetServiceEntries.Add(new() { VehicleId = id, ServiceDate = Service.Date!.Value.Date, Description = Service.Description!.Trim() });
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Vehicle {id}: scheduled service" });
        await db.SaveChangesAsync();
        return RedirectToPage(new { vehicle = id });
    }

    public async Task<IActionResult> OnPostServiceStatusAsync(int id, int entryId, bool completed)
    {
        if (!await OperationsPolicy.Vehicles(db).AnyAsync(v => v.Id == id)) return NotFound();
        var entry = await db.FleetServiceEntries.SingleOrDefaultAsync(s => s.Id == entryId && s.VehicleId == id);
        if (entry == null) return NotFound();
        entry.Completed = completed;
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Vehicle {id}: service {entryId} {(completed ? "completed" : "reopened")}" });
        await db.SaveChangesAsync();
        return RedirectToPage(new { vehicle = id });
    }

    private const string CsvColumns = "Plate,Name,BranchId,DriverId,OdometerKm,RegistrationExpires,InsuranceExpires,InspectionDue,Status";

    public IActionResult OnGetSample(string? dataset)
    {
        if (dataset == "service")
            return RegistrationCsv.Download("vehicle-service-sample", "VehicleId,Date,Description",
                [[1, BusinessClock.Today.AddDays(1), "Oil change"]]);
        return RegistrationCsv.Download("vehicles-sample", CsvColumns, [["G-001", "Office vehicle", 1, null, 0, null, null, null, VehicleState.Available]]);
    }

    public async Task<IActionResult> OnGetExportAsync(string? dataset)
    {
        if (dataset == "service")
        {
            var vehicles = OperationsPolicy.Vehicles(db);
            var service = await db.FleetServiceEntries.AsNoTracking()
                .Where(x => vehicles.Any(v => v.Id == x.VehicleId)).OrderBy(x => x.ServiceDate).ToListAsync();
            Response.Headers.CacheControl = "no-store";
            return RegistrationCsv.Download("vehicle-service", "VehicleId,Date,Description",
                service.Select(x => new object?[] { x.VehicleId, x.ServiceDate, x.Description }));
        }
        var records = await OperationsPolicy.Vehicles(db).AsNoTracking().OrderBy(x => x.Plate).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("vehicles", CsvColumns, records.Select(x => new object?[] { x.Plate, x.Name, x.BranchId, x.DriverId, x.OdometerKm, x.RegistrationExpires, x.InsuranceExpires, x.InspectionDue, x.Status }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload, string? dataset)
    {
        if (dataset == "service")
            return RegistrationCsv.ImportAsync(this, db, upload, "VehicleId,Date,Description", async row =>
            {
                var id = row.Get<int>("VehicleId");
                var input = new ServiceInput { Date = row.Get<DateTime>("Date").Date, Description = row.Get<string>("Description") };
                TryValidateModel(input, "Service");
                if (await db.FleetServiceEntries.AnyAsync(x => x.VehicleId == id && x.ServiceDate == input.Date && x.Description == input.Description))
                    throw new FormatException("This service entry already exists.");
                return await OnPostServiceAsync(id, input);
            });
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            var input = new VehicleInput();
            input.Plate = row.Get<string>("Plate");
            input.Name = row.Get<string>("Name");
            input.BranchId = row.Get<int>("BranchId");
            input.DriverId = row.Get<int?>("DriverId");
            input.OdometerKm = row.Get<int>("OdometerKm");
            input.RegistrationExpires = row.Get<DateTime?>("RegistrationExpires");
            input.InsuranceExpires = row.Get<DateTime?>("InsuranceExpires");
            input.InspectionDue = row.Get<DateTime?>("InspectionDue");
            input.Status = row.Get<VehicleState>("Status");
            TryValidateModel(input, "Input");
            return await OnPostSaveAsync(input);
        });
    }
}
