using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace HrSystemDhg.Web.Services;
public static class OperationsPolicy
{
    public static IQueryable<ApplicationUser> EligiblePeople(HrDbContext db, string? module = null)
    {
        var users = db.Users.Where(u => u.IsEnabled);
        if (db.Scoped)
        {
            var visible = db.VisibleEmployeeIds;
            users = users.Where(u => u.EmployeeId.HasValue && visible.Contains(u.EmployeeId.Value) || u.Id == db.ActorId);
        }
        if (module != null)
            users = users.Where(u => !db.UserModuleRestrictions.Any(r => r.UserId == u.Id && r.Module == module) ||
                db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(role => role.Id == ur.RoleId && role.NormalizedName == "ADMIN")));
        return users;
    }
    public static IQueryable<FleetVehicle> Vehicles(HrDbContext db)
    {
        var branches = db.AllowedBranchIds;
        return db.Scoped ? db.FleetVehicles.Where(v => branches.Contains(v.BranchId)) : db.FleetVehicles;
    }
    public static IQueryable<WorkTask> Tasks(HrDbContext db, string actor) =>
        db.WorkTasks.Where(t => t.OwnerId == actor || t.AssigneeId == actor);
    public static IQueryable<WorkMeeting> Meetings(HrDbContext db, string actor) =>
        db.WorkMeetings.Where(m => m.OwnerId == actor || m.Participants.Any(p => p.UserId == actor));

    public static bool IsJoinUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Host switch
        {
            "meet.google.com" => uri.AbsolutePath.Trim('/').Length > 0 && uri.AbsolutePath != "/new",
            "teams.microsoft.com" or "teams.live.com" or "teams.cloud.microsoft" =>
                uri.AbsolutePath.StartsWith("/l/meetup-join/", StringComparison.Ordinal) ||
                uri.AbsolutePath.StartsWith("/meet/", StringComparison.Ordinal),
            _ => false
        };

    public static string HashTrackerKey(string key) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(key)));
    public static bool MatchesTrackerKey(string? hash, string key) => hash is { Length: 64 } &&
        key.Length == 64 && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(HashTrackerKey(key)));

    public static bool ValidFix(GpsFix fix, DateTimeOffset now) =>
        fix.Latitude is >= -90 and <= 90 && fix.Longitude is >= -180 and <= 180 &&
        (fix.SpeedKph is null or (>= 0 and <= 400)) && fix.RecordedAt is DateTimeOffset time &&
        time >= now.AddDays(-30) && time <= now.AddMinutes(5);

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Mogadishu");
    public static DateTime ToUtc(DateTime date) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), Zone);
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
}
public sealed record GpsFix(double? Latitude, double? Longitude, DateTimeOffset? RecordedAt, double? SpeedKph);
