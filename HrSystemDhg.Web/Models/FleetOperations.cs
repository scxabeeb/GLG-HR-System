using System.ComponentModel.DataAnnotations;
namespace HrSystemDhg.Web.Models;

public enum FleetTripState { Requested, Approved, InProgress, Completed, Rejected, Cancelled }
public enum FleetCostType { Fuel, Maintenance, Insurance, Registration, Inspection, Other }

public class FleetTrip
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    [MaxLength(255)] public string RequesterId { get; set; } = "";
    public int? VehicleId { get; set; }
    public FleetVehicle? Vehicle { get; set; }
    public int? DriverId { get; set; }
    public Employee? Driver { get; set; }
    [MaxLength(160)] public string Destination { get; set; } = "";
    [MaxLength(1000)] public string Purpose { get; set; } = "";
    public DateTime StartsUtc { get; set; }
    public DateTime EndsUtc { get; set; }
    public DateTime? DepartedUtc { get; set; }
    public DateTime? ReturnedUtc { get; set; }
    public int? StartKm { get; set; }
    public int? EndKm { get; set; }
    public FleetTripState Status { get; set; }
    [MaxLength(500)] public string? DecisionNote { get; set; }
}

public class FleetExpense
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public FleetVehicle Vehicle { get; set; } = null!;
    public int BranchId { get; set; }
    public DateTime Date { get; set; }
    public FleetCostType Type { get; set; }
    [MaxLength(3)] public string Currency { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal? Litres { get; set; }
    [MaxLength(250)] public string? Reference { get; set; }
}
