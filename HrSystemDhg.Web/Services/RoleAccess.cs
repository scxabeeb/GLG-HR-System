using System.Security.Claims;
using HrSystemDhg.Web.Data;
using Microsoft.EntityFrameworkCore;
namespace HrSystemDhg.Web.Services;

public sealed class RoleAccess(HrDbContext db)
{
    public const string BaseRoleClaim="garowe:base-role";
    public const string DeniedMenuClaim="garowe:menu-deny";
    public const string AllowedMenuClaim="garowe:menu-allow";
    public const string DerivedIssuer="GaroweRole";
    public static readonly string[] BuiltInRoles=["Admin","HR","Management","Finance","General Secretary","Chairman","Employee"];
    public static readonly string[] CapabilityRoles=BuiltInRoles.Where(r=>r!="Admin").ToArray();
    public static HashSet<string> Keys=>ModuleAccess.Modules.Select(m=>m.Key).Concat(MenuPermissions.Items.Select(m=>m.Key)).ToHashSet();
    public static bool BuiltIn(string? name)=>BuiltInRoles.Contains(name,StringComparer.OrdinalIgnoreCase);
    public static HashSet<string> Combine(IEnumerable<string> roleDenied,IEnumerable<string> userDenied,IEnumerable<string> userAllowed)
    {
        var result=roleDenied.Concat(userDenied).Where(Keys.Contains).ToHashSet();
        result.ExceptWith(userAllowed.Where(Keys.Contains));
        // A current explicit user denial wins over an obsolete conflicting Allow claim.
        result.UnionWith(userDenied.Where(Keys.Contains));
        return result;
    }
    public async Task<HashSet<string>> RoleDeniedAsync(string actor)
    {
        return (await (from membership in db.UserRoles
            join claim in db.RoleClaims on membership.RoleId equals claim.RoleId
            where membership.UserId==actor && claim.ClaimType==DeniedMenuClaim && claim.ClaimValue!=null
            select claim.ClaimValue!).AsNoTracking().ToListAsync()).Where(Keys.Contains).ToHashSet();
    }
    public async Task ApplyCapabilitiesAsync(ClaimsPrincipal principal)
    {
        foreach(var identity in principal.Identities)
            foreach(var claim in identity.Claims.Where(c=>c.Issuer==DerivedIssuer).ToArray())identity.RemoveClaim(claim);
        var authenticated=principal.Identities.FirstOrDefault(i=>i.IsAuthenticated);
        var actor=authenticated?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if(actor==null)return;
        var claims=await (from membership in db.UserRoles join role in db.Roles on membership.RoleId equals role.Id
            join claim in db.RoleClaims on role.Id equals claim.RoleId
            where membership.UserId==actor && claim.ClaimType==BaseRoleClaim
            select new{role.Name,claim.ClaimValue}).AsNoTracking().ToListAsync();
        foreach(var name in claims.Where(c=>!BuiltIn(c.Name) && CapabilityRoles.Contains(c.ClaimValue)).Select(c=>c.ClaimValue!).Distinct())
            if(!principal.IsInRole(name))authenticated!.AddClaim(new(authenticated.RoleClaimType,name,ClaimValueTypes.String,DerivedIssuer));
    }
}

public sealed record PermissionMatrix(Dictionary<string,string> Choices,bool IncludeInherit,HashSet<string>? InheritedDenied=null);
