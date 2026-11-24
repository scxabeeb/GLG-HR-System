using System.ComponentModel.DataAnnotations;
namespace HrSystemDhg.Web.Models;
public sealed class ZkPunchReview
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    [MaxLength(64)] public string Pin { get; set; } = "";
    public DateTime RecordedTime { get; set; }
    public DateTime ReceivedUtc { get; set; }
    [MaxLength(250)] public string Reason { get; set; } = "";
    public DateTime? ReviewedUtc { get; set; }
    public DateTime? CorrectedTime { get; set; }
    [MaxLength(20)] public string? Resolution { get; set; }
    [MaxLength(500)] public string? ReviewReason { get; set; }
    [MaxLength(256)] public string? Reviewer { get; set; }
}
