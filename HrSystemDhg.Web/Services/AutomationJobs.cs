using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;
namespace HrSystemDhg.Web.Services;
public static class AutomationJobs
{
    public sealed record Input(string Kind);
    public static async Task<IResult> Run(Input input,HttpContext ctx,HrDbContext db){
        ctx.Response.Headers.CacheControl="no-store";
        var config=await db.AutomationSettings.AsNoTracking().SingleOrDefaultAsync(s=>s.Id==1);
        if(config?.Enabled!=true||!config.AllowReminders||!AutomationSecurity.Matches(ctx.Request.Headers["X-Garowe-Reminder-Key"].FirstOrDefault(),config.ReminderKeyHash))return Results.Unauthorized();
        var now=DateTime.UtcNow;var today=BusinessClock.Today;var added=0;
        async Task Queue(string key,string recipient,string title,OperationsNoticeKind kind,int? task=null,int? meeting=null){
            if(await db.AutomationReminders.AnyAsync(r=>r.Id==key))return;
            db.AutomationReminders.Add(new(){Id=key});db.OperationsNotifications.Add(new(){RecipientId=recipient,Title=title[..Math.Min(title.Length,160)],Kind=kind,TaskId=task,MeetingId=meeting,CreatedUtc=now,WhatsApp=WhatsAppDelivery.InAppOnly});
            try{await db.SaveChangesAsync();added++;}catch(DbUpdateException error)when(error.InnerException is MySqlConnector.MySqlException{Number:1062}){db.ChangeTracker.Clear();}
        }
        switch(input.Kind){
            case "tasks" when config.Tasks:
                foreach(var task in await db.WorkTasks.AsNoTracking().Where(t=>t.DueDate!=null&&t.DueDate<=today&&t.Status!=TaskProgress.Done&&t.Status!=TaskProgress.Cancelled&&db.Users.Any(u=>u.Id==t.AssigneeId&&u.IsEnabled)).OrderBy(t=>t.DueDate).Take(200).ToListAsync())
                    await Queue($"task:{task.Id}:{task.DueDate:yyyyMMdd}:{today:yyyyMMdd}",task.AssigneeId,(task.DueDate<today?"Overdue task: ":"Task due today: ")+task.Title,OperationsNoticeKind.TaskProgress,task.Id);
                break;
            case "meetings" when config.Meetings:
                foreach(var meeting in await db.WorkMeetings.AsNoTracking().Where(m=>!m.Cancelled&&m.EndedUtc==null&&m.StartsUtc>=now&&m.StartsUtc<=now.AddMinutes(30)).Take(50).ToListAsync()){
                    var users=await db.Set<MeetingParticipant>().Where(p=>p.MeetingId==meeting.Id&&db.Users.Any(u=>u.Id==p.UserId&&u.IsEnabled)).Select(p=>p.UserId).Distinct().Take(500).ToListAsync();
                    foreach(var user in users)await Queue($"meeting:{meeting.Id}:{meeting.StartsUtc.Ticks}:30:{user}",user,"Meeting starts within 30 minutes: "+meeting.Title,OperationsNoticeKind.MeetingUpdated,meeting:meeting.Id);
                }break;
            case "daily":
                if(BusinessClock.Now.Hour<16||today.DayOfWeek==DayOfWeek.Friday)return Results.Ok(new{queued=0,message="Daily digests run after 4 PM East Africa Time, Saturday–Thursday."});
                var modules=new List<string>();
                if(config.Tasks)modules.Add("Tasks overdue: "+await db.WorkTasks.CountAsync(t=>t.DueDate<today&&t.Status!=TaskProgress.Done&&t.Status!=TaskProgress.Cancelled));
                if(config.Meetings)modules.Add("Meetings today: "+await db.WorkMeetings.CountAsync(m=>m.StartsUtc>=today.AddHours(-3)&&m.StartsUtc<today.AddDays(1).AddHours(-3)&&!m.Cancelled));
                if(config.Attendance)modules.Add("Incomplete attendance: "+await db.AttendanceDays.CountAsync(a=>a.Date==today&&a.MissingMinutes>0&&!db.LeaveRequests.Any(l=>l.EmployeeId==a.EmployeeId&&l.Status=="Approved"&&l.From<=a.Date&&l.To>=a.Date)));
                if(config.Payroll)modules.Add("Payroll awaiting approval: "+await db.PayrollPeriods.CountAsync(p=>p.Status=="Submitted"));
                var admins=await (from role in db.Roles join membership in db.UserRoles on role.Id equals membership.RoleId join user in db.Users on membership.UserId equals user.Id where role.Name=="Admin"&&user.IsEnabled select user.Id).Distinct().ToListAsync();
                foreach(var admin in admins)await Queue($"daily:{today:yyyyMMdd}:{admin}",admin,string.Join(" · ",modules),OperationsNoticeKind.StaffMessage);
                break;
            default:return Results.Forbid();
        }
        return Results.Ok(new{queued=added,kind=input.Kind,timeZone="Africa/Mogadishu"});
    }
}
