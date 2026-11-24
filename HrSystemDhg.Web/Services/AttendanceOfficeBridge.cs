using HrSystemDhg.Web.Data;
using Microsoft.EntityFrameworkCore;
namespace HrSystemDhg.Web.Services;

public static class AttendanceOfficeBridge
{
    public sealed record Identity(int DeviceId, string Hash, string Address);
    public static void Map(WebApplication app) => app.MapMethods("/api/attendance/bridge/{id:int}/{operation}", ["GET", "POST"], Receive)
        .AllowAnonymous().RequireRateLimiting("attendance-push");
    private static async Task<IResult> Receive(int id, string operation, HttpContext context, HrDbContext db,
        IWebHostEnvironment env, AttendanceService attendance)
    {
        if (operation is not ("heartbeat" or "cdata" or "getrequest" or "devicecmd") ||
            (operation is "heartbeat" or "getrequest") && !HttpMethods.IsGet(context.Request.Method) ||
            operation == "devicecmd" && !HttpMethods.IsPost(context.Request.Method)) return Results.NotFound();
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal) || authorization.Length != 71) return Results.Unauthorized();
        var device = await db.ZkDevices.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, context.RequestAborted);
        if (device?.PushSerial == null || device.PushAddress == null || !OperationsPolicy.MatchesTrackerKey(device.OfficeConnectorKeyHash, authorization[7..]) ||
            ZkAttendancePush.Serial(context.Request.Headers["X-Terminal-Serial"].ToString()) != device.PushSerial ||
            context.Request.Headers["X-Terminal-Address"].ToString() != device.PushAddress) return Results.Unauthorized();
        var now = DateTime.UtcNow;
        await db.ZkDevices.Where(d => d.Id == id && d.OfficeConnectorKeyHash == device.OfficeConnectorKeyHash)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastOfficeConnectorSeenUtc, now), context.RequestAborted);
        if (operation == "heartbeat") return Results.Ok(new { connected = true, deviceId = id, terminalLastContactUtc = device.LastPushSeenUtc, lastUploadUtc = device.LastSuccess });
        context.Items[typeof(AttendanceOfficeBridge)] = new Identity(device.Id, device.OfficeConnectorKeyHash!, device.PushAddress);
        return operation switch {
            "getrequest" => await ZkAttendancePush.NextCommand(context, db, env),
            "devicecmd" => await ZkAttendancePush.CommandResult(context, db, env),
            _ => HttpMethods.IsGet(context.Request.Method) ? await ZkAttendancePush.Poll(context, db, env) : await ZkAttendancePush.Upload(context, db, env, attendance)
        };
    }
}
