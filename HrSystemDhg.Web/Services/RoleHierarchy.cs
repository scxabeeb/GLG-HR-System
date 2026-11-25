using System.Security.Claims;

namespace HrSystemDhg.Web.Services;

public static class RoleHierarchy
{
    // Derived per authenticated request, so revoking General Secretary also removes inherited access.
    public static void Apply(ClaimsPrincipal user)
    {
        foreach (var current in user.Identities)
            foreach (var inherited in current.Claims.Where(c => c.Issuer == "RoleHierarchy" && c.Type == current.RoleClaimType && c.Value == "Management").ToArray())
                current.RemoveClaim(inherited);
        var identity = user.Identities.FirstOrDefault(i => i.IsAuthenticated && i.HasClaim(i.RoleClaimType, "General Secretary"));
        if (identity != null && !user.IsInRole("Management"))
            identity.AddClaim(new Claim(identity.RoleClaimType, "Management", ClaimValueTypes.String, "RoleHierarchy"));
    }
}

public sealed class RoleHierarchyClaimsTransformation : Microsoft.AspNetCore.Authentication.IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        RoleHierarchy.Apply(principal);
        return Task.FromResult(principal);
    }
}
