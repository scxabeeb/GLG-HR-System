using System.ComponentModel.DataAnnotations;
namespace HrSystemDhg.Web.Models;

public class TaskActivity
{
    public long Id { get; set; }
    public int TaskId { get; set; }
    public WorkTask Task { get; set; } = null!;
    [MaxLength(255)] public string ActorId { get; set; } = "";
    [MaxLength(32)] public string Action { get; set; } = "";
    [MaxLength(255)] public string? ChangedFields { get; set; }
    public TaskProgress? PreviousStatus { get; set; }
    public TaskProgress? NewStatus { get; set; }
    public DateTime RecordedUtc { get; set; }
}
