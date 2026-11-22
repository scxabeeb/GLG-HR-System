using System.ComponentModel.DataAnnotations;

namespace HrSystemDhg.Web.Models;

public enum AssetStatus
{
    Available, InUse, UnderRepair, Depreciated, Lost, Stolen, Retired, Disposed
}

public class Asset
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string Tag { get; set; } = "";
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required, StringLength(80)] public string Category { get; set; } = "";
    [StringLength(120)] public string? Brand { get; set; }
    [StringLength(120)] public string? Model { get; set; }
    [StringLength(120)] public string? SerialNumber { get; set; }
    [StringLength(150)] public string? Supplier { get; set; }
    [StringLength(120)] public string? PurchaseReference { get; set; }
    [DataType(DataType.Date)] public DateTime? PurchasedOn { get; set; }
    [DataType(DataType.Date)] public DateTime? WarrantyUntil { get; set; }
    [Range(typeof(decimal), "0", "999999999999.99")] public decimal? PurchaseCost { get; set; }
    [Range(typeof(decimal), "0", "999999999999.99")] public decimal? BookValue { get; set; }
    [Required, RegularExpression("[A-Za-z]{3}"), StringLength(3)] public string Currency { get; set; } = "USD";
    [Range(1, int.MaxValue)] public int BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int? EmployeeId { get; set; }
    [StringLength(150)] public string? OfficeLocation { get; set; }
    [StringLength(100)] public string? Condition { get; set; }
    public AssetStatus Status { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    [ConcurrencyCheck] public Guid Version { get; set; }
    public AssetPhoto? Photo { get; set; }
}

public class AssetPhoto
{
    [Key] public int AssetId { get; set; }
    [StringLength(20)] public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];
}

public class AssetActivity
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public int BranchId { get; set; }
    public DateTime RecordedUtc { get; set; }
    [StringLength(256)] public string Actor { get; set; } = "";
    [StringLength(30)] public string Action { get; set; } = "";
    public AssetStatus Status { get; set; }
    public int? EmployeeId { get; set; }
    [StringLength(150)] public string? OfficeLocation { get; set; }
    public decimal? BookValue { get; set; }
    [StringLength(3)] public string Currency { get; set; } = "";
    [StringLength(500)] public string? Note { get; set; }
}
