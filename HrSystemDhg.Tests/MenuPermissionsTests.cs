using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

public class MenuPermissionsTests
{
    [Fact]
    public void DenyingOneSubmenuPreservesOtherSubmenusAndParentDenialOverridesAll()
    {
        HashSet<string> denied = ["tasks.manage", "payroll.reports"];
        Assert.False(MenuPermissions.Allows(denied,"tasks.manage"));
        Assert.True(MenuPermissions.Allows(denied,"tasks.mine"));
        Assert.True(MenuPermissions.Allows(denied,"tasks.reports"));
        Assert.False(MenuPermissions.Allows(denied,"payroll.reports"));
        Assert.True(MenuPermissions.Allows(denied,"payroll.salary"));
        denied.Add("tasks");
        Assert.False(MenuPermissions.Allows(denied,"tasks.mine"));
    }
    [Theory]
    [InlineData("/Operations/Tasks", "?handler=Save&view=mine", "POST", "tasks.manage")]
    [InlineData("/Operations/Tasks", "?handler=Save&handler=Progress", "POST", "tasks.manage")]
    [InlineData("/Operations/Tasks", "?view=created&view=mine", "GET", "tasks.manage")]
    [InlineData("/Assets", "?report=TRUE", "GET", "assets.reports")]
    [InlineData("/Operations/Tasks", "?view=created", "GET", "tasks.manage")]
    [InlineData("/Operations/Tasks", "?handler=Progress", "POST", "tasks.mine")]
    [InlineData("/Operations/Meetings", "?handler=End", "POST", "meetings.manage")]
    [InlineData("/Operations/MeetingDetails", "?handler=FollowUp", "POST", "meetings.manage")]
    [InlineData("/Operations/MeetingDetails", "?handler=Agenda", "POST", "meetings.mine")]
    [InlineData("/Reports", "?report=Bank", "GET", "payroll.reports")]
    [InlineData("/Reports", "?report=Balances", "GET", "leave.reports")]
    [InlineData("/api/mobile/v2/manage/tasks", "", "POST", "tasks.manage")]
    [InlineData("/api/mobile/v2/manage/options", "?module=meetings", "GET", "meetings.manage")]
    [InlineData("/api/mobile/v2/meetings/1/end", "", "POST", "meetings.manage")]
    [InlineData("/Assets", "?report=true", "GET", "assets.reports")]
    public void RoutesAndActionsUseTheSamePermission(string path,string query,string method,string key) =>
        Assert.Equal(key,MenuPermissions.ForUrl(path,new QueryCollection(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query)),method));
    [Fact]
    public void CatalogFitsPersistedKeysAndHasNoDuplicates()
    {
        Assert.Equal(MenuPermissions.Items.Length,MenuPermissions.Items.Select(x=>x.Key).Distinct().Count());
        Assert.All(MenuPermissions.Items,x=>{
            Assert.True(x.Key.Length<=32);
            Assert.Contains(ModuleAccess.Modules,m=>m.Key==x.Module);
        });
    }
}
