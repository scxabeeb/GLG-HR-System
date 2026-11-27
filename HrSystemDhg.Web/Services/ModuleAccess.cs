using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;

public sealed class ModuleAccess(HrDbContext db)
{
    public static readonly (string Key, string Label)[] Modules =
    [
        ("people", "Employee records"), ("attendance", "Attendance management"), ("leave", "Leave management"),
        ("payroll", "Salary and payroll"), ("reports", "Reports and exports"), ("personal", "Employee self-service"),
        ("tasks", "Task manager"), ("meetings", "Meeting organizer"), ("fleet", "Vehicle fleet"), ("assets", "Asset management")
    ];

    private HashSet<string>? blocked;
    public async Task<HashSet<string>> BlockedAsync()
    {
        if (blocked != null) return blocked;
        var actor = db.ActorId;
        if(actor==null || db.IsAdmin)return blocked=[];
        var roleDenied=await new RoleAccess(db).RoleDeniedAsync(actor);
        var userDenied=await db.UserModuleRestrictions.AsNoTracking().Where(r=>r.UserId==actor).Select(r=>r.Module).ToListAsync();
        var userAllowed=await db.UserClaims.AsNoTracking().Where(c=>c.UserId==actor && c.ClaimType==RoleAccess.AllowedMenuClaim && c.ClaimValue!=null).Select(c=>c.ClaimValue!).ToListAsync();
        blocked=RoleAccess.Combine(roleDenied,userDenied,userAllowed);
        return blocked;
    }

    public static string? ForRoute(string? area, string? page)
    {
        return (area?.ToLowerInvariant(), page?.ToLowerInvariant()) switch
        {
            ("hr", "/attendance" or "/corrections") => "attendance",
            ("hr", "/leave" or "/balances") => "leave",
            ("hr", "/salary") or ("management", "/payroll") => "payroll",
            ("hr" or "management", "/dashboard") or (_, "/index") => "overview",
            ("hr", _) => "people",
            (_, "/assets/index" or "/assets/edit") => "assets",
            (_, "/reports/index") => "reports",
            (_, "/me/profile" or "/me/payslip" or "/me/approvals" or "/me/agenda") => "personal",
            (_, "/files/download") => "documents",
            (_, "/operations/tasks" or "/operations/taskreport") => "tasks",
            (_, "/operations/meetings" or "/operations/meetingroom" or "/operations/meetingreport" or "/operations/meetingrecordings" or "/operations/meetingdetails") => "meetings",
            (_, "/operations/fleet" or "/operations/fleettrips" or "/operations/fleetreport") => "fleet",
            _ => null
        };
    }

    public static bool Allows(HashSet<string> denied, string? module) => module switch
    {
        "overview" => !denied.Overlaps(["people", "attendance", "leave", "payroll", "reports", "personal"]),
        "reports" => !denied.Overlaps(["people", "attendance", "leave", "payroll", "reports"]),
        "documents" => !denied.Overlaps(["people", "leave", "personal"]),
        null => true,
        _ => !denied.Contains(module)
    };
}
