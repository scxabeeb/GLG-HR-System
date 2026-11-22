using System.ComponentModel.DataAnnotations;

namespace HrSystemDhg.Web.Models;

public sealed class ZkDeviceCommand
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    [MaxLength(64)] public string Serial { get; set; } = "";
    [MaxLength(45)] public string Address { get; set; } = "";
    [MaxLength(9)] public string Pin { get; set; } = "";
    [MaxLength(64)] public string Name { get; set; } = "";
    public int? FingerSlot { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? SentUtc { get; set; }
    public int? Result { get; set; }
}
