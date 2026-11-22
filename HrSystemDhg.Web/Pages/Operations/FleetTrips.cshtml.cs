using System.ComponentModel.DataAnnotations;
using System.Data;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace HrSystemDhg.Web.Pages.Operations;

public class FleetTripsModel(HrDbContext db, OperationsNotices notices) : PageModel
{
    public bool CanManage => FleetAccess.CanManage(db, User);
    public string Actor => db.ActorId!;
    public int? EmployeeId { get; private set; }
    public List<Branch> RequestBranches { get; private set; } = [];
    public Dictionary<int,string> BranchNames { get; private set; } = [];
    public List<FleetTrip> Trips { get; private set; } = [];
    public List<FleetVehicle> Vehicles { get; private set; } = [];
    public Dictionary<int,string> Drivers { get; private set; } = [];
    public RequestInput Input { get; private set; } = new();
    [BindProperty(SupportsGet = true)] public FleetTripState? Status { get; set; }
    [BindProperty(SupportsGet = true)] public bool Mine { get; set; }

    public class RequestInput
    {
        [Range(1, int.MaxValue)] public int BranchId { get; set; }
        [Required, StringLength(160)] public string Destination { get; set; } = "";
        [Required, StringLength(1000)] public string Purpose { get; set; } = "";
        [Required] public DateTime? Start { get; set; }
        [Required] public DateTime? End { get; set; }
    }

    private async Task LoadActor()
    {
        EmployeeId = await db.Users.Where(u => u.Id == Actor).Select(u => u.EmployeeId).SingleOrDefaultAsync();
    }
    private IQueryable<Branch> BranchesForRequest()
    {
        var scope = db.AllowedBranchIds;
        var today = BusinessClock.Today;
        if (CanManage)
            return db.Branchs.Where(b => b.IsActive && (!db.Scoped || scope.Contains(b.Id)));
        var employee = EmployeeId;
        return db.Branchs.Where(b => b.IsActive && employee.HasValue &&
            db.EmployeeAssignments.IgnoreQueryFilters().Any(a => a.EmployeeId == employee && a.BranchId == b.Id &&
                a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today)));
    }

    public async Task OnGetAsync()
    {
        Response.Headers.CacheControl = "no-store";
        await LoadActor();
        RequestBranches = await BranchesForRequest().AsNoTracking().OrderBy(b => b.Name).ToListAsync();
        var query = CanManage && !Mine ? FleetAccess.ManagedTrips(db) :
            db.FleetTrips.Where(t => t.RequesterId == Actor || (EmployeeId.HasValue && t.DriverId == EmployeeId));
        if (Status.HasValue) query = query.Where(t => t.Status == Status);
        // Load display names separately; employee query filters must not hide trip rows.
        Trips = await query.AsNoTracking().OrderBy(t => t.Status >= FleetTripState.Completed)
            .ThenByDescending(t => t.StartsUtc).Take(200).ToListAsync();
        var branchIds = Trips.Select(t => t.BranchId).Distinct().ToArray();
        BranchNames = await db.Branchs.Where(b => branchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name);
        if (CanManage && !Mine)
        {
            Vehicles = await OperationsPolicy.Vehicles(db).AsNoTracking().Where(v => v.Status == VehicleState.Available || v.Status == VehicleState.InService)
                .OrderBy(v => v.Plate).ToListAsync();
            Drivers = await db.Employees.Where(e => e.Status == "Active" || e.Status == "Probation")
                .OrderBy(e => e.FullName).ToDictionaryAsync(e => e.Id, e => e.FullName);
        }
        else
        {
            var ids = Trips.Where(t => t.VehicleId.HasValue).Select(t => t.VehicleId!.Value).Distinct().ToArray();
            Vehicles = await db.FleetVehicles.Where(v => ids.Contains(v.Id)).Select(v => new FleetVehicle { Id = v.Id, Plate = v.Plate, Name = v.Name }).ToListAsync();
        }
    }

    public async Task<IActionResult> OnPostRequestAsync([Bind(Prefix = "Input")] RequestInput input)
    {
        Input = input;
        await LoadActor();
        if (input.Start is not { Year: >= 1900 and < 9999 } || input.End is not { Year: >= 1900 and < 9999 } ||
            input.End <= input.Start || input.Start < BusinessClock.Now.AddMinutes(-15) || input.End - input.Start > TimeSpan.FromDays(30))
            ModelState.AddModelError("Input.Start", "Select a valid upcoming trip of up to 30 days.");
        if (!await BranchesForRequest().AnyAsync(b => b.Id == input.BranchId))
            ModelState.AddModelError("Input.BranchId", "Select an active branch available to your account.");
        if (!ModelState.IsValid) { await OnGetAsync(); return Page(); }
        db.FleetTrips.Add(new() { BranchId = input.BranchId, RequesterId = Actor, Destination = input.Destination.Trim(),
            Purpose = input.Purpose.Trim(), StartsUtc = OperationsPolicy.ToUtc(input.Start!.Value),
            EndsUtc = OperationsPolicy.ToUtc(input.End!.Value) });
        db.AuditLogs.Add(new() { Actor = Actor, Action = "Requested a fleet trip" });
        await db.SaveChangesAsync();
        return Done("Trip request submitted.");
    }

    public async Task<IActionResult> OnPostDecideAsync(int id, int? vehicleId, int? driverId, bool? approve, string? note)
    {
        if (!CanManage) return Forbid();
        if (!ModelState.IsValid || approve == null || note?.Length > 500) return BadRequest();
        return await Change(async () =>
        {
            var trip = await FleetAccess.ManagedTrips(db).SingleOrDefaultAsync(t => t.Id == id);
            if (trip == null) return NotFound();
            if (trip.Status != FleetTripState.Requested) return Done("Only a pending request can be decided.");
            if (approve == true)
            {
                var vehicle = await OperationsPolicy.Vehicles(db).SingleOrDefaultAsync(v => v.Id == vehicleId && v.BranchId == trip.BranchId);
                if (vehicle == null || vehicle.Status is VehicleState.Maintenance or VehicleState.Retired)
                    return Done("Choose an operating vehicle from the trip branch.");
                if (trip.EndsUtc <= DateTime.UtcNow) return Done("This request has already expired.");
                var date = OperationsPolicy.Local(trip.StartsUtc).Date;
                var validDriver = await db.Employees.AnyAsync(e => e.Id == driverId && (e.Status == "Active" || e.Status == "Probation") &&
                    db.EmployeeAssignments.Any(a => a.EmployeeId == e.Id && a.BranchId == trip.BranchId &&
                        a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date)));
                if (!validDriver) return Done("Choose an active driver assigned to the trip branch.");
                if (await db.FleetTrips.AnyAsync(t => t.Id != id && (t.VehicleId == vehicleId || t.DriverId == driverId) &&
                    (t.Status == FleetTripState.InProgress || t.Status == FleetTripState.Approved && t.StartsUtc < trip.EndsUtc && t.EndsUtc > trip.StartsUtc)))
                    return Done("The vehicle or driver is already booked for that time.");
                trip.VehicleId = vehicleId;
                trip.DriverId = driverId;
            }
            trip.Status = approve == true ? FleetTripState.Approved : FleetTripState.Rejected;
            trip.DecisionNote = note?.Trim();
            return await Save(trip, approve == true ? "Trip approved." : "Trip rejected.");
        });
    }

    public async Task<IActionResult> OnPostMoveAsync(int id, string action, int? odometer)
    {
        await LoadActor();
        return await Change(async () =>
        {
            var managed = CanManage && await FleetAccess.ManagedTrips(db).AnyAsync(t => t.Id == id);
            var trip = await db.FleetTrips.SingleOrDefaultAsync(t => t.Id == id);
            if (trip == null || !managed && trip.RequesterId != Actor && !(EmployeeId.HasValue && trip.DriverId == EmployeeId)) return NotFound();
            if (action == "cancel" && (managed || trip.RequesterId == Actor) && trip.Status is FleetTripState.Requested or FleetTripState.Approved)
            {
                trip.Status = FleetTripState.Cancelled;
                return await Save(trip, "Trip cancelled.");
            }
            if (!managed && !(EmployeeId.HasValue && trip.DriverId == EmployeeId)) return Forbid();
            if (odometer is not (>= 0 and <= 9999999)) return Done("Enter a valid odometer reading.");
            var vehicle = await db.FleetVehicles.SingleOrDefaultAsync(v => v.Id == trip.VehicleId && v.BranchId == trip.BranchId);
            if (vehicle == null || odometer < vehicle.OdometerKm) return Done("The reading cannot be below the vehicle’s recorded mileage.");
            if (action == "start" && trip.Status == FleetTripState.Approved)
            {
                if (DateTime.UtcNow < trip.StartsUtc.AddHours(-1) || DateTime.UtcNow >= trip.EndsUtc)
                    return Done("Departure can be logged within an hour before the planned start and before the planned end.");
                if (vehicle.Status != VehicleState.Available || await db.FleetTrips.AnyAsync(t => t.Id != id &&
                    (t.VehicleId == trip.VehicleId || t.DriverId == trip.DriverId) && t.Status == FleetTripState.InProgress))
                    return Done("The vehicle or driver is already in service or unavailable.");
                trip.StartKm = odometer;
                trip.DepartedUtc = DateTime.UtcNow;
                trip.Status = FleetTripState.InProgress;
                vehicle.Status = VehicleState.InService;
            }
            else if (action == "complete" && trip.Status == FleetTripState.InProgress && odometer >= trip.StartKm)
            {
                trip.EndKm = odometer;
                trip.ReturnedUtc = DateTime.UtcNow;
                trip.Status = FleetTripState.Completed;
                if (vehicle.Status == VehicleState.InService) vehicle.Status = VehicleState.Available;
            }
            else return Done("That action is not available for this trip.");
            vehicle.OdometerKm = odometer.Value;
            return await Save(trip, action == "start" ? "Departure recorded." : "Return and mileage recorded.");
        });
    }

    private async Task<IActionResult> Save(FleetTrip trip, string message)
    {
        db.AuditLogs.Add(new() { Actor = Actor, Action = $"Fleet trip {trip.Id}: {trip.Status}" });
        await notices.AddFleetTripAsync(trip, Actor);
        await db.SaveChangesAsync();
        return Done(message);
    }

    private async Task<IActionResult> Change(Func<Task<IActionResult>> action)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var result = await action();
            await tx.CommitAsync();
            return result;
        }
        catch (Exception ex) when (ex is MySqlException { Number: 1205 or 1213 } ||
            ex is DbUpdateException { InnerException: MySqlException { Number: 1205 or 1213 } })
        { return Done("Another fleet update was in progress. Reload and try again."); }
    }

    private IActionResult Done(string message)
    {
        TempData["OperationsNotice"] = message;
        return RedirectToPage(new { Mine, Status });
    }

    private const string CsvColumns = "BranchId,Destination,Purpose,Start,End";

    public IActionResult OnGetSample()
    {
        return RegistrationCsv.Download("trip-requests-sample", CsvColumns, [[1, "Municipal office", "Site visit", BusinessClock.Today.AddDays(1).AddHours(9), BusinessClock.Today.AddDays(1).AddHours(11)]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var records = await db.FleetTrips.AsNoTracking().Where(x => x.RequesterId == Actor).OrderBy(x => x.StartsUtc).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("trip-requests", CsvColumns, records.Select(x => new object?[] { x.BranchId, x.Destination, x.Purpose, OperationsPolicy.Local(x.StartsUtc), OperationsPolicy.Local(x.EndsUtc) }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload)
    {
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            var input = new RequestInput();
            input.BranchId = row.Get<int>("BranchId");
            input.Destination = row.Get<string>("Destination");
            input.Purpose = row.Get<string>("Purpose");
            input.Start = row.Get<DateTime?>("Start");
            input.End = row.Get<DateTime?>("End");
            TryValidateModel(input, "Input");
            return await OnPostRequestAsync(input);
        });
    }
}
