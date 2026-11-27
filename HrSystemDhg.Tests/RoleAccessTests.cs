using HrSystemDhg.Web.Services;
using Xunit;
public class RoleAccessTests
{
    [Fact]
    public void RoleDenialsCombineAndUserOverridesWin()
    {
        var denied=RoleAccess.Combine(["tasks.manage","payroll.manage"],["meetings.manage"],["tasks.manage"]);
        Assert.DoesNotContain("tasks.manage",denied);Assert.Contains("payroll.manage",denied);Assert.Contains("meetings.manage",denied);
    }
    [Fact]
    public void ConflictingExplicitDenyAndParentDenialRemainDenied()
    {
        var denied=RoleAccess.Combine(["tasks"],["tasks.manage"],["tasks.mine","tasks.manage"]);
        Assert.Contains("tasks.manage",denied);Assert.False(MenuPermissions.Allows(denied,"tasks.mine"));
    }
    [Fact]
    public void UnknownPermissionKeysAreIgnoredAndAdministratorIsNotACustomCapability()
    {
        Assert.Empty(RoleAccess.Combine(["invented"],[],["Admin"]));
        Assert.DoesNotContain("Admin",RoleAccess.CapabilityRoles);Assert.True(RoleAccess.BuiltIn("aDmIn"));
    }
    [Fact]
    public void ExistingSharedReportRestrictionsRemainProtected()
    {
        HashSet<string> denied=["people"];
        Assert.False(ModuleAccess.Allows(denied,"reports"));Assert.False(MenuPermissions.Allows(denied,"people.reports"));
        Assert.True(MenuPermissions.Allows(denied,"attendance.reports"));
    }
}
