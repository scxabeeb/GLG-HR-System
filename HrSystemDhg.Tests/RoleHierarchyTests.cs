using System.Security.Claims;
using HrSystemDhg.Web.Services;
using Xunit;

public class RoleHierarchyTests
{
    [Fact]
    public void SecretaryInheritsManagementWithoutAdministrativeOrHrPrivileges()
    {
        var user = Principal("General Secretary");
        RoleHierarchy.Apply(user);RoleHierarchy.Apply(user);
        Assert.True(user.IsInRole("Management"));
        Assert.True(user.IsInRole("General Secretary"));
        Assert.False(user.IsInRole("Admin"));Assert.False(user.IsInRole("HR"));
        Assert.Single(user.FindAll(ClaimTypes.Role),x=>x.Value=="Management");
    }
    [Theory]
    [InlineData("Employee")]
    [InlineData("HR")]
    [InlineData("Finance")]
    public void OtherRolesDoNotGainManagement(string role)
    {
        var user = Principal(role);RoleHierarchy.Apply(user);
        Assert.False(user.IsInRole("Management"));
    }
    [Fact]
    public void UnauthenticatedClaimsDoNotGrantInheritedAccess()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role,"General Secretary")]));
        RoleHierarchy.Apply(user);Assert.False(user.IsInRole("Management"));
    }
    [Fact]
    public void RemovingSecretaryDropsInheritedManagementButPreservesExplicitManagement()
    {
        var user = Principal("General Secretary");RoleHierarchy.Apply(user);
        var identity = (ClaimsIdentity)user.Identity!;
        identity.RemoveClaim(identity.FindFirst(c=>c.Type==ClaimTypes.Role && c.Value=="General Secretary")!);
        RoleHierarchy.Apply(user);Assert.False(user.IsInRole("Management"));
        identity.AddClaim(new Claim(ClaimTypes.Role,"Management"));
        RoleHierarchy.Apply(user);Assert.True(user.IsInRole("Management"));
    }
    private static ClaimsPrincipal Principal(string role) => new(new ClaimsIdentity([new Claim(ClaimTypes.Role,role)],"test"));
}
