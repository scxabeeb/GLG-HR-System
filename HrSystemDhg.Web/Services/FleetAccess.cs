using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;

namespace HrSystemDhg.Web.Services;
public static class FleetAccess
{
    public static bool CanManage(HrDbContext db, System.Security.Claims.ClaimsPrincipal user) => db.IsAdmin || user.IsInRole("Management");
    public static IQueryable<FleetTrip> ManagedTrips(HrDbContext db)
    {
        var ids = db.AllowedBranchIds;
        return db.Scoped ? db.FleetTrips.Where(t => ids.Contains(t.BranchId)) : db.FleetTrips;
    }
    public static IQueryable<FleetExpense> ManagedExpenses(HrDbContext db)
    {
        var ids = db.AllowedBranchIds;
        return db.Scoped ? db.FleetExpenses.Where(t => ids.Contains(t.BranchId)) : db.FleetExpenses;
    }
}
