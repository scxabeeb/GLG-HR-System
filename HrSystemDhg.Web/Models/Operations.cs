using System.ComponentModel.DataAnnotations;
namespace HrSystemDhg.Web.Models;

public enum TaskProgress { Open, InProgress, Done, Cancelled }
public enum TaskPriority { Low, Normal, High, Urgent }
public enum VehicleState { Available, InService, Maintenance, Retired }
public class WorkTaskTeam
{
    public int Id { get; set; }
    public List<WorkTaskTeamMember> Members { get; set; } = [];
    [MaxLength(2500)] public string? Brief { get; set; }
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(255)] public string OwnerId { get; set; } = "";
}
public class WorkTaskTeamMember
{
    public int TeamId { get; set; }
    [MaxLength(255)] public string UserId { get; set; } = "";
}
public class WorkSubtask
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    [MaxLength(1000)] public string Responsibility { get; set; } = "";
}
public class WorkTask
{
    public int Id { get; set; }
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(2500)] public string Description { get; set; } = "";
    [MaxLength(255)] public string OwnerId { get; set; } = "";
    [MaxLength(255)] public string AssigneeId { get; set; } = "";
    public DateTime? DueDate { get; set; }
    public TaskPriority Priority { get; set; }
    public TaskProgress Status { get; set; }
    public DateTime? UpdatedUtc { get; set; }
    public int? SourceMeetingId { get; set; }
    public int? TeamId { get; set; }
    public WorkTaskTeam? Team { get; set; }
    public int? SubtaskId { get; set; }
    public WorkSubtask? Subtask { get; set; }
    [MaxLength(1000)] public string? Responsibility { get; set; }
}
public class WorkMeeting
{
    public int Id { get; set; }
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(2500)] public string Agenda { get; set; } = "";
    [MaxLength(250)] public string? Location { get; set; }
    [MaxLength(12000), System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "text")] public string? MinutesDraft { get; set; }
    [MaxLength(12000), System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "text")] public string? PublishedMinutes { get; set; }
    public DateTime? MinutesPublishedUtc { get; set; }
    [MaxLength(255)] public string OwnerId { get; set; } = "";
    [MaxLength(2048)] public string JoinUrl { get; set; } = "";
    public DateTime StartsUtc { get; set; }
    public DateTime EndsUtc { get; set; }
    public DateTime? EndedUtc { get; set; }
    public bool Cancelled { get; set; }
    public List<MeetingParticipant> Participants { get; set; } = [];
}
public class MeetingParticipant
{
    public DateTime? AttendedUtc { get; set; }
    public int MeetingId { get; set; }
    [MaxLength(255)] public string UserId { get; set; } = "";
}
public class FleetVehicle
{
    public int Id { get; set; }
    [MaxLength(32)] public string Plate { get; set; } = "";
    [MaxLength(120)] public string Name { get; set; } = "";
    public int BranchId { get; set; }
    public int? DriverId { get; set; }
    public int OdometerKm { get; set; }
    public DateTime? RegistrationExpires { get; set; }
    public DateTime? InsuranceExpires { get; set; }
    public DateTime? InspectionDue { get; set; }
    public VehicleState Status { get; set; }
    [MaxLength(64)] public string? TrackerKeyHash { get; set; }
}
public class FleetPosition
{
    public long Id { get; set; }
    public int VehicleId { get; set; }
    public DateTime RecordedUtc { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? SpeedKph { get; set; }
}
public class FleetServiceEntry
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public DateTime ServiceDate { get; set; }
    [MaxLength(500)] public string Description { get; set; } = "";
    public bool Completed { get; set; }
}
