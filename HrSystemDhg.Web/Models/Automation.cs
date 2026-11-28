using System.ComponentModel.DataAnnotations;
namespace HrSystemDhg.Web.Models;
public class AutomationSettings
{
    public int Id { get; set; } = 1;
    public bool Enabled { get; set; }
    [MaxLength(2048)] public string WebhookUrl { get; set; } = "";
    [MaxLength(2048)] public string ProtectedWebhookSecret { get; set; } = "";
    [MaxLength(64)] public string? CallbackKeyHash { get; set; }
    public Guid Generation { get; set; } = Guid.NewGuid();
    public bool Tasks { get; set; } = true;
    public bool Meetings { get; set; } = true;
    public bool Attendance { get; set; } = true;
    public bool Payroll { get; set; } = true;
    [MaxLength(64)] public string? ReminderKeyHash { get; set; }
    public bool AllowReminders { get; set; }
    public bool IncludeContacts { get; set; }
    public bool IncludePayrollAmounts { get; set; }
}
public class AutomationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(24)] public string Module { get; set; } = "";
    [MaxLength(80)] public string EventType { get; set; } = "";
    public Guid Generation { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string Payload { get; set; } = "{}";
    [MaxLength(20)] public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime NextAttemptUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? DeliveredUtc { get; set; }
    public int? LastHttpStatus { get; set; }
    [MaxLength(250)] public string? LastError { get; set; }
}


public class AutomationReminder {
    [Key,MaxLength(180)] public string Id {get;set;}="";
    public DateTime CreatedUtc {get;set;}=DateTime.UtcNow;
}
