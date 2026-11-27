using System.Security.Claims;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
public static class AccessControlChecks
{
    public static async Task RunAsync(string connection,Action<bool,string> check)
    {
        var services=new ServiceCollection();services.AddLogging();services.AddDataProtection().UseEphemeralDataProtectionProvider();services.AddHttpContextAccessor();
        services.AddDbContext<HrDbContext>(o=>o.UseMySql(connection,new MySqlServerVersion(new Version(8,0,0))));
        services.AddIdentityCore<ApplicationUser>(o=>PasswordPolicy.Configure(o.Password)).AddRoles<IdentityRole>().AddEntityFrameworkStores<HrDbContext>().AddDefaultTokenProviders();
        using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<HrDbContext>();var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach(var password in new[]{"1234","aaaa","ABCD","!!!!"})
        {
            var results=await Task.WhenAll(users.PasswordValidators.Select(v=>v.ValidateAsync(users,new ApplicationUser(),password)));
            check(results.All(r=>r.Succeeded),"four-character passwords have no composition requirements");
        }
        var shortResults=await Task.WhenAll(users.PasswordValidators.Select(v=>v.ValidateAsync(users,new ApplicationUser(),"abc")));
        check(shortResults.Any(r=>r.Errors.Any(e=>e.Code=="PasswordTooShort")),"passwords shorter than four characters are rejected");
        foreach(var name in RoleAccess.BuiltInRoles)if(!await roles.RoleExistsAsync(name))await roles.CreateAsync(new(name));
        var suffix=Guid.NewGuid().ToString("N")[..8];
        var admin=new ApplicationUser{UserName="access-admin-"+suffix,Email="access-admin-"+suffix+"@example.test"};
        check((await users.CreateAsync(admin,"Fixture!AccessOld123")).Succeeded,"access-control administrator fixture created");await users.AddToRoleAsync(admin,"Admin");
        var account=new ApplicationUser{UserName="access-employee-"+suffix,Email="access-employee-"+suffix+"@example.test"};await users.CreateAsync(account,"Fixture!AccessOld123");await users.AddToRoleAsync(account,"Employee");
        var employee=new Employee{EmployeeNumber="ACCESS-"+suffix,FullName="Linked role fixture",WorkEmail="linked-"+suffix+"@example.test"};db.Add(employee);await db.SaveChangesAsync();account.EmployeeId=employee.Id;await users.UpdateAsync(account);
        var http=new DefaultHttpContext{RequestServices=scope.ServiceProvider};var accessor=scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();accessor.HttpContext=http;
        ClaimsPrincipal Principal(string id,params string[] assigned)=>new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,id),new Claim(ClaimTypes.Name,"access-fixture")}.Concat(assigned.Select(r=>new Claim(ClaimTypes.Role,r))),"test"));
        void Administrator()=>http.User=Principal(admin.Id,"Admin");Administrator();
        var rolePage=new HrSystemDhg.Web.Pages.Admin.RolesModel(db,roles){PageContext=new PageContext{HttpContext=http},TempData=new TempDataDictionary(http,new NavigationTempDataProvider())};
        await rolePage.OnPostCreateAsync("Coordinator "+suffix,["Employee","HR","Finance"]);
        var custom=await roles.FindByNameAsync("Coordinator "+suffix)??throw new Exception("Custom role was not saved");check(true,"administrator creates a custom role with multiple operational capabilities");
        var matrix=RoleAccess.Keys.ToDictionary(k=>k,k=>"Allow");matrix["tasks.manage"]="Deny";
        await rolePage.OnPostSaveAsync(custom.Id,["Employee","HR","Finance"],matrix);
        var userPage=new HrSystemDhg.Web.Pages.Admin.UsersModel(db,users){PageContext=new PageContext{HttpContext=http},TempData=new TempDataDictionary(http,new NavigationTempDataProvider())};
        var oldStamp=account.SecurityStamp;await userPage.OnPostRolesAsync(account.Id,["Employee",custom.Name!,"Management"]);await db.Entry(account).ReloadAsync();
        check(account.EmployeeId==employee.Id && account.SecurityStamp!=oldStamp && (await users.GetRolesAsync(account)).Count==3,"multiple account roles preserve the employee profile and invalidate old sessions");
        var principal=Principal(account.Id,"Employee",custom.Name!,"Management");http.User=principal;
        var transform=new RoleHierarchyClaimsTransformation(db);await transform.TransformAsync(principal);await transform.TransformAsync(principal);
        check(principal.IsInRole("HR") && principal.IsInRole("Finance") && !principal.IsInRole("Admin") && principal.FindAll(ClaimTypes.Role).Count(c=>c.Value=="HR")==1,"custom capabilities apply once per request without granting administrator access");
        var denied=await new ModuleAccess(db).BlockedAsync();check(denied.Contains("tasks.manage") && MenuPermissions.Allows(denied,"tasks.mine"),"role submenu denial preserves other submenus");
        Administrator();var overrides=RoleAccess.Keys.ToDictionary(k=>k,k=>"Inherit");overrides["tasks.manage"]="Allow";await userPage.OnPostMenuAccessAsync(account.Id,overrides);
        http.User=principal;check(!(await new ModuleAccess(db).BlockedAsync()).Contains("tasks.manage"),"individual Allow can override an inherited role denial");
        Administrator();overrides["tasks.manage"]="Deny";await userPage.OnPostMenuAccessAsync(account.Id,overrides);http.User=principal;
        check((await new ModuleAccess(db).BlockedAsync()).Contains("tasks.manage"),"individual Deny revokes menu access");
        Administrator();overrides["tasks.manage"]="Inherit";await userPage.OnPostMenuAccessAsync(account.Id,overrides);matrix["tasks"]="Deny";await rolePage.OnPostSaveAsync(custom.Id,["Employee"],matrix);
        http.User=principal;await transform.TransformAsync(principal);check(!principal.IsInRole("HR")&&!principal.IsInRole("Finance"),"removing custom capabilities revokes derived privileges on an existing principal");
        check(!await OperationsPolicy.EligiblePeople(db,"tasks").AnyAsync(u=>u.Id==account.Id),"task assignment excludes users denied by their role's parent menu");
        Administrator();overrides["tasks"]="Allow";overrides["tasks.mine"]="Allow";await userPage.OnPostMenuAccessAsync(account.Id,overrides);http.User=principal;
        check(await OperationsPolicy.EligiblePeople(db,"tasks").AnyAsync(u=>u.Id==account.Id),"explicit user overrides restore eligible assignment access consistently");
        Administrator();await roles.AddClaimAsync(custom,new(RoleAccess.BaseRoleClaim,"Admin"));http.User=principal;await transform.TransformAsync(principal);
        check(!principal.IsInRole("Admin"),"unexpected administrator capability claims cannot escalate a custom role");
        Administrator();check(await rolePage.OnPostDeleteAsync("missing-role") is NotFoundResult,"unknown custom-role deletion is rejected");
        check(await rolePage.OnPostDeleteAsync((await roles.FindByNameAsync("Admin"))!.Id) is BadRequestObjectResult,"built-in administrator role cannot be deleted");
        check(await rolePage.OnPostDeleteAsync(custom.Id) is PageResult,"assigned custom roles must be unassigned before deletion");
        check(await userPage.OnPostRolesAsync(admin.Id,["Employee"]) is BadRequestObjectResult && await users.IsInRoleAsync(admin,"Admin"),"administrator cannot remove their own Admin role");
        check(await userPage.OnPostToggleAsync(admin.Id) is BadRequestObjectResult,"administrator cannot deactivate their own account");
        oldStamp=account.SecurityStamp;await userPage.OnPostResetPasswordAsync(account.Id,"Fixture!AccessReset456","Fixture!AccessReset456");await db.Entry(account).ReloadAsync();
        check(await users.CheckPasswordAsync(account,"Fixture!AccessReset456")&&!await users.CheckPasswordAsync(account,"Fixture!AccessOld123")&&account.MustChangePassword&&account.SecurityStamp!=oldStamp,"administrator password reset replaces the password, forces change and revokes sessions");
        await userPage.OnPostToggleAsync(account.Id);check(!account.IsEnabled,"administrator can mark an account inactive");await userPage.OnPostToggleAsync(account.Id);check(account.IsEnabled,"administrator can reactivate an account");
        check(!await db.AuditLogs.AnyAsync(a=>a.Action.Contains("Fixture!AccessReset456")),"password-reset audit never records the password");
        await userPage.OnPostRolesAsync(account.Id,["Employee"]);check(await rolePage.OnPostDeleteAsync(custom.Id) is RedirectToPageResult,"unused custom role can be deleted after unassignment");
    }
}
