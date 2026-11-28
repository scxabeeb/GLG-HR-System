using System.Text.Json;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace HrSystemDhg.Web.Services;
public static class AutomationCapture
{
    public static string? Module(object item) => item switch {
        WorkTask or WorkSubtask or WorkTaskTeam => "tasks",
        WorkMeeting or MeetingParticipant => "meetings",
        AttendanceDay or ZkPunch => "attendance",
        PayrollPeriod or Payslip => "payroll", _=>null
    };
    public static bool Allowed(AutomationSettings config,string module) => module switch {
        "tasks"=>config.Tasks,"meetings"=>config.Meetings,"attendance"=>config.Attendance,"payroll"=>config.Payroll,_=>false
    };
    public static AutomationEvent Capture(EntityEntry entry,AutomationSettings config,string? actor,EntityState state,string[] changes)
    {
        var kind=entry.Entity switch {WorkTask=>"task",WorkTaskTeam=>"team",WorkSubtask=>"subtask",WorkMeeting=>"meeting",MeetingParticipant=>"participant",AttendanceDay=>"attendance.day",ZkPunch=>"attendance.punch",PayrollPeriod=>"payroll.period",Payslip=>"payslip",_=>"record"};
        var type=kind+ (state==EntityState.Added?".created":state==EntityState.Deleted?".deleted":".updated");
        // Deliberately omit passwords, biometrics, bank details, free-form minutes and per-person salary amounts.
        object data=entry.Entity switch {
            WorkTask t=>new {t.Id,t.Title,t.Status,t.Priority,t.AssigneeId,t.OwnerId,t.DueDate,t.TeamId,t.SubtaskId,t.SourceMeetingId},
            WorkTaskTeam t=>new {t.Id,t.Title,t.OwnerId},
            WorkSubtask t=>new {t.Id,t.TeamId,t.Responsibility},
            WorkMeeting m=>new {m.Id,m.Title,m.OwnerId,StartsUtc=DateTime.SpecifyKind(m.StartsUtc,DateTimeKind.Utc),EndsUtc=DateTime.SpecifyKind(m.EndsUtc,DateTimeKind.Utc),EndedUtc=m.EndedUtc.HasValue?(DateTime?)DateTime.SpecifyKind(m.EndedUtc.Value,DateTimeKind.Utc):null,m.Cancelled,m.Location,m.MinutesPublishedUtc},
            MeetingParticipant p=>new {p.MeetingId,p.UserId,p.AttendedUtc},
            AttendanceDay a=>new {a.Id,a.EmployeeId,a.Date,a.FirstIn,a.LastOut,a.WorkedHours,a.ExpectedHours,a.LateMinutes,a.EarlyMinutes,a.MissingMinutes,a.Status,a.StatusMessage},
            ZkPunch p=>new {p.Id,p.DeviceId,p.Pin,p.PunchTime},
            PayrollPeriod p=>new {p.Id,p.From,p.To,p.Status,p.Revision,p.WorkingDays},
            Payslip p=>new {p.Id,p.PayrollLineId,p.IsPublished},_=>new{}
        };
        var result=new AutomationEvent{Module=Module(entry.Entity)!,EventType=type,Generation=config.Generation};
        result.Payload=JsonSerializer.Serialize(new{schemaVersion=1,eventId=result.Id,eventType=type,module=result.Module,occurredUtc=result.CreatedUtc,timeZone="Africa/Mogadishu",actorId=actor,changedFields=changes,data},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}});
        return result;
    }
}
