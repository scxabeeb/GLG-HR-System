using System.Text.Json;
using HrSystemDhg.Web.Data;
using Microsoft.EntityFrameworkCore;
namespace HrSystemDhg.Web.Services;
public static class AutomationApi
{
    static async Task<Models.AutomationSettings?> Access(HttpContext ctx,HrDbContext db) {
        ctx.Response.Headers.CacheControl="no-store";
        var config=await db.AutomationSettings.AsNoTracking().SingleOrDefaultAsync(s=>s.Id==1);
        return config?.Enabled==true&&AutomationSecurity.Matches(ctx.Request.Headers["X-Garowe-Automation-Key"].FirstOrDefault(),config.CallbackKeyHash)?config:null;
    }
    public static void Map(WebApplication app) {
        app.MapPost("/api/integrations/n8n/reminders",AutomationJobs.Run).AllowAnonymous().RequireRateLimiting("fleet-gps");
        app.MapGet("/api/integrations/n8n/summary",Summary).AllowAnonymous().RequireRateLimiting("fleet-gps");
        app.MapGet("/api/integrations/n8n/recipients",Recipients).AllowAnonymous().RequireRateLimiting("fleet-gps");
    }
    static async Task<IResult> Summary(string module,HttpContext ctx,HrDbContext db) {
        var config=await Access(ctx,db);if(config==null)return Results.Unauthorized();
        if(!AutomationCapture.Allowed(config,module))return Results.StatusCode(403);
        var now=DateTime.UtcNow;var today=BusinessClock.Today;
        object rows;
        switch(module){
            case "tasks": rows=await db.WorkTasks.AsNoTracking().Where(t=>t.Status!=Models.TaskProgress.Done&&t.Status!=Models.TaskProgress.Cancelled&&(t.DueDate==null||t.DueDate<=today.AddDays(1))).OrderBy(t=>t.DueDate).Take(500).Select(t=>new{t.Id,t.Title,t.DueDate,t.OwnerId,t.AssigneeId,Status=t.Status.ToString()}).ToListAsync();break;
            case "meetings": rows=await db.WorkMeetings.AsNoTracking().Where(m=>!m.Cancelled&&m.EndedUtc==null&&m.EndsUtc>=now&&m.StartsUtc<=now.AddDays(1)).OrderBy(m=>m.StartsUtc).Take(500).Select(m=>new{m.Id,m.Title,StartsUtc=DateTime.SpecifyKind(m.StartsUtc,DateTimeKind.Utc),EndsUtc=DateTime.SpecifyKind(m.EndsUtc,DateTimeKind.Utc),m.OwnerId,m.Location}).ToListAsync();break;
            case "attendance": rows=await db.AttendanceDays.AsNoTracking().Where(a=>a.Date==today).OrderBy(a=>a.EmployeeId).Take(500).Select(a=>new{a.Id,a.EmployeeId,a.Date,a.FirstIn,a.LastOut,a.WorkedHours,a.ExpectedHours,a.MissingMinutes,a.Status,a.StatusMessage,ApprovedLeave=db.LeaveRequests.Any(l=>l.EmployeeId==a.EmployeeId&&l.Status=="Approved"&&l.From<=a.Date&&l.To>=a.Date)}).ToListAsync();break;
            case "payroll":
                var periods=await db.PayrollPeriods.AsNoTracking().Where(p=>p.To>=today.AddMonths(-3)).OrderByDescending(p=>p.From).Take(100).Select(p=>new{p.Id,p.From,p.To,p.Status,p.Revision}).ToListAsync();
                rows=config.IncludePayrollAmounts?periods.Select(p=>new{period=p,gross=db.PayrollLines.Where(l=>l.PayrollPeriodId==p.Id).Sum(l=>(decimal?)l.Gross)??0,net=db.PayrollLines.Where(l=>l.PayrollPeriodId==p.Id).Sum(l=>(decimal?)l.Net)??0}).ToArray():periods;break;
            default:return Results.BadRequest(new{message="Choose tasks, meetings, attendance or payroll."});
        }
        return Results.Ok(new{schemaVersion=1,module,timeZone="Africa/Mogadishu",generatedUtc=now,date=today,limit=500,items=rows});
    }
    static async Task<IResult> Recipients(Guid eventId,HttpContext ctx,HrDbContext db) {
        var config=await Access(ctx,db);if(config==null)return Results.Unauthorized();
        var item=await db.AutomationEvents.AsNoTracking().SingleOrDefaultAsync(e=>e.Id==eventId);if(item==null)return Results.NotFound();
        if(!config.IncludeContacts||!AutomationCapture.Allowed(config,item.Module))return Results.StatusCode(403);
        using var payload=JsonDocument.Parse(item.Payload);var data=payload.RootElement.GetProperty("data");
        var ids=new List<string>();int? employeeId=null;
        foreach(var name in new[]{"assigneeId","ownerId","userId"})if(data.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String&&value.GetString() is string id)ids.Add(id);
        if(item.Module=="meetings"&&data.TryGetProperty("id",out var meeting)&&meeting.TryGetInt32(out var meetingId))ids.AddRange(await db.Set<Models.MeetingParticipant>().Where(p=>p.MeetingId==meetingId).Select(p=>p.UserId).ToListAsync());
        if(data.TryGetProperty("employeeId",out var employee)&&employee.TryGetInt32(out var eid))employeeId=eid;
        if(item.Module=="attendance"&&data.TryGetProperty("pin",out var pin))employeeId=await db.Employees.Where(e=>e.ZkPin==pin.GetString()).Select(e=>(int?)e.Id).FirstOrDefaultAsync();
        if(item.Module=="payroll"&&data.TryGetProperty("payrollLineId",out var line)&&line.TryGetInt32(out var lineId))employeeId=await db.PayrollLines.Where(l=>l.Id==lineId).Select(l=>(int?)l.EmployeeId).FirstOrDefaultAsync();
        var people=await (from user in db.Users join emp in db.Employees on user.EmployeeId equals (int?)emp.Id where user.IsEnabled&&(ids.Contains(user.Id)||employeeId!=null&&user.EmployeeId==employeeId) select new {user.Id,emp.FullName,emp.WorkEmail,emp.Phone}).Take(500).ToListAsync();
        return Results.Ok(new{eventId,recipients=people.Select(p=>new{userId=p.Id,name=p.FullName,email=p.WorkEmail,whatsapp=OperationsNotices.NormalizePhone(p.Phone)})});
    }
}
