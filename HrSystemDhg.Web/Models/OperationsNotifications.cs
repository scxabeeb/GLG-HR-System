using System.ComponentModel.DataAnnotations;

namespace HrSystemDhg.Web.Models;

public enum OperationsNoticeKind { TaskAssigned, TaskProgress, MeetingInvitation, MeetingUpdated, MeetingCancelled, FleetTripUpdated, MeetingMinutesPublished, TeamAssigned, AssetAssigned, LeaveDecided, PayslipPublished, AttendanceCorrectionDecided, StaffMessage }
public enum WhatsAppDelivery { InAppOnly, Queued, Sending, Accepted, Skipped, Failed, Unknown }

public class OperationsNotification
{
    public long Id { get; set; }
    [MaxLength(255)] public string RecipientId { get; set; } = "";
    [MaxLength(160)] public string Title { get; set; } = "";
    public OperationsNoticeKind Kind { get; set; }
    public TaskProgress? Progress { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? ReadUtc { get; set; }
    public int? TaskId { get; set; }
    public WorkTask? Task { get; set; }
    public int? MeetingId { get; set; }
    public WorkMeeting? Meeting { get; set; }
    public int? FleetTripId { get; set; }
    public FleetTrip? FleetTrip { get; set; }
    public WhatsAppDelivery WhatsApp { get; set; }
    public DateTime? AttemptUtc { get; set; }
}
public class WhatsAppPreference
{
    [MaxLength(255)] public string UserId { get; set; } = "";
    [MaxLength(16)] public string Number { get; set; } = "";
    public DateTime OptedInUtc { get; set; }
}
