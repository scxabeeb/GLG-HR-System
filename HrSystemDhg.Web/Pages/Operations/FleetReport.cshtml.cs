using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Operations;

[Authorize(Roles = "Admin,Management")]
public class FleetReportModel(HrDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true)] public int? VehicleId { get; set; }
    public List<FleetVehicle> Vehicles { get; private set; } = [];
    public List<FleetTrip> Trips { get; private set; } = [];
    public List<FleetExpense> Expenses { get; private set; } = [];
    public List<FleetServiceEntry> PendingService { get; private set; } = [];
    public Dictionary<FleetTripState,int> TripCounts { get; private set; } = [];
    public List<(string Currency, decimal Total)> CostTotals { get; private set; } = [];
    public long Km { get; private set; }
    public decimal Litres { get; private set; }
    public ExpenseInput Expense { get; private set; } = new() { Date = BusinessClock.Today };
    public DateTime RenewalCutoff => BusinessClock.Today.AddDays(30);

    public class ExpenseInput
    {
        [Range(1, int.MaxValue)] public int VehicleId { get; set; }
        [Required] public DateTime? Date { get; set; }
        [EnumDataType(typeof(FleetCostType))] public FleetCostType Type { get; set; }
        [Required, RegularExpression("[A-Za-z]{3}")] public string Currency { get; set; } = "USD";
        [Required, Range(typeof(decimal), "0.01", "999999999")] public decimal? Amount { get; set; }
        [Range(typeof(decimal), "0.01", "100000")] public decimal? Litres { get; set; }
        [StringLength(250)] public string? Reference { get; set; }
    }

    private bool RangeIsValid()
    {
        From ??= BusinessClock.Today.AddDays(-29);
        To ??= BusinessClock.Today;
        if (From.Value.Year < 1900 || To.Value.Year >= 9999 || To.Value.Date < From.Value.Date ||
            To.Value.Date - From.Value.Date > TimeSpan.FromDays(365))
            ModelState.AddModelError("", "Choose a valid reporting period of up to 366 days.");
        return ModelState.IsValid;
    }

    private IQueryable<FleetTrip> TripQuery()
    {
        var start = OperationsPolicy.ToUtc(From!.Value.Date);
        var end = OperationsPolicy.ToUtc(To!.Value.Date.AddDays(1));
        return FleetAccess.ManagedTrips(db).AsNoTracking().Where(t => t.StartsUtc >= start && t.StartsUtc < end &&
            (!VehicleId.HasValue || t.VehicleId == VehicleId));
    }
    private IQueryable<FleetExpense> ExpenseQuery()
    {
        var start = From!.Value.Date;
        var end = To!.Value.Date;
        return FleetAccess.ManagedExpenses(db).AsNoTracking().Where(e => e.Date >= start && e.Date <= end &&
            (!VehicleId.HasValue || e.VehicleId == VehicleId));
    }

    public async Task<IActionResult> OnGetAsync()
    {
        Response.Headers.CacheControl = "no-store";
        Vehicles = await OperationsPolicy.Vehicles(db).AsNoTracking().OrderBy(v => v.Plate).ToListAsync();
        if (!RangeIsValid()) return Page();
        var trips = TripQuery();
        var expenses = ExpenseQuery();
        TripCounts = await trips.GroupBy(t => t.Status).Select(g => new { State = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.State, x => x.Count);
        Km = await trips.Where(t => t.Status == FleetTripState.Completed && t.EndKm.HasValue && t.StartKm.HasValue)
            .SumAsync(t => (long?)t.EndKm - t.StartKm) ?? 0;
        Litres = await expenses.SumAsync(e => e.Litres) ?? 0;
        CostTotals = (await expenses.GroupBy(e => e.Currency).Select(g => new { Currency = g.Key, Total = g.Sum(e => e.Amount) })
            .OrderBy(x => x.Currency).ToListAsync()).Select(x => (x.Currency, x.Total)).ToList();
        Trips = await trips.Include(t => t.Vehicle).OrderByDescending(t => t.StartsUtc).Take(150).ToListAsync();
        Expenses = await expenses.Include(e => e.Vehicle).OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).Take(150).ToListAsync();
        var ids = Vehicles.Where(v => !VehicleId.HasValue || v.Id == VehicleId).Select(v => v.Id).ToArray();
        PendingService = await db.FleetServiceEntries.Where(s => ids.Contains(s.VehicleId) && !s.Completed && s.ServiceDate <= RenewalCutoff)
            .OrderBy(s => s.ServiceDate).Take(100).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostExpenseAsync([Bind(Prefix = "Expense")] ExpenseInput input)
    {
        Expense = input;
        var vehicle = await OperationsPolicy.Vehicles(db).SingleOrDefaultAsync(v => v.Id == input.VehicleId);
        if (vehicle == null) ModelState.AddModelError("Expense.VehicleId", "Select a vehicle in your branch scope.");
        if (input.Date?.Year < 1900 || input.Date > BusinessClock.Today)
            ModelState.AddModelError("Expense.Date", "Select an expense date on or before today.");
        if (input.Litres.HasValue && input.Type != FleetCostType.Fuel)
            ModelState.AddModelError("Expense.Litres", "Fuel volume can only be entered for fuel expenses.");
        if (!ModelState.IsValid) return await OnGetAsync();

        db.FleetExpenses.Add(new() { VehicleId = vehicle!.Id, BranchId = vehicle.BranchId, Date = input.Date!.Value.Date,
            Type = input.Type, Currency = input.Currency.ToUpperInvariant(), Amount = input.Amount!.Value,
            Litres = input.Litres, Reference = input.Reference?.Trim() });
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Logged {input.Type} expense for vehicle {vehicle.Id}" });
        await db.SaveChangesAsync();
        TempData["OperationsNotice"] = "Expense saved.";
        return RedirectToPage(new { From, To, VehicleId });
    }

    public async Task<IActionResult> OnGetCsvAsync(string report, string format = "csv")
    {
        Response.Headers.CacheControl = "no-store";
        if (!RangeIsValid()) return BadRequest("Invalid date range.");
        if (report == "expenses")
        {
            var list = await ExpenseQuery().Include(e => e.Vehicle).OrderByDescending(e => e.Date).Take(10001).ToListAsync();
            if (list.Count > 10000) return BadRequest("Choose a narrower range to export up to 10,000 rows.");
            var rows = new List<string[]> { new[] { "Date", "Vehicle", "Category", "Currency", "Amount", "Fuel litres", "Reference" } };
            rows.AddRange(list.Select(e => new[] { Date(e.Date), e.Vehicle.Plate, e.Type.ToString(), e.Currency,
                e.Amount.ToString(CultureInfo.InvariantCulture), e.Litres?.ToString(CultureInfo.InvariantCulture) ?? "", e.Reference ?? "" }));
            return ExportService.Report(rows, "fleet-expenses", format);
        }
        if (report != "trips") return BadRequest();
        var trips = await TripQuery().Include(t => t.Vehicle).OrderByDescending(t => t.StartsUtc).Take(10001).ToListAsync();
        if (trips.Count > 10000) return BadRequest("Choose a narrower range to export up to 10,000 rows.");
        var records = new List<string[]> { new[] { "Trip ID", "Destination", "Vehicle", "Status", "Start (EAT)", "End (EAT)", "Distance (km)" } };
        records.AddRange(trips.Select(t => new[] { t.Id.ToString(), t.Destination, t.Vehicle?.Plate ?? "", t.Status.ToString(),
            OperationsPolicy.Local(t.StartsUtc).ToString("yyyy-MM-dd HH:mm"), OperationsPolicy.Local(t.EndsUtc).ToString("yyyy-MM-dd HH:mm"),
            (t.EndKm - t.StartKm)?.ToString(CultureInfo.InvariantCulture) ?? "" }));
        return ExportService.Report(records, "fleet-trips", format);
    }

    private static string Date(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private const string CsvColumns = "VehicleId,Date,Type,Currency,Amount,Litres,Reference";

    public IActionResult OnGetSample()
    {
        return RegistrationCsv.Download("fleet-expenses-sample", CsvColumns, [[1, BusinessClock.Today, FleetCostType.Fuel, "USD", 50, 20, "Example receipt"]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var records = await FleetAccess.ManagedExpenses(db).AsNoTracking().OrderBy(x => x.Date).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("fleet-expenses", CsvColumns, records.Select(x => new object?[] { x.VehicleId, x.Date, x.Type, x.Currency, x.Amount, x.Litres, x.Reference }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload)
    {
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            var input = new ExpenseInput();
            input.VehicleId = row.Get<int>("VehicleId");
            input.Date = row.Get<DateTime?>("Date");
            input.Type = row.Get<FleetCostType>("Type");
            input.Currency = row.Get<string>("Currency");
            input.Amount = row.Get<decimal?>("Amount");
            input.Litres = row.Get<decimal?>("Litres");
            input.Reference = row.Optional("Reference");
            TryValidateModel(input, "Expense");
            return await OnPostExpenseAsync(input);
        });
    }
}
