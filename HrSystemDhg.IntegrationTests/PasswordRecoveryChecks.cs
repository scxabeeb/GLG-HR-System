using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
public static class PasswordRecoveryChecks
{
 public static async Task RunAsync(string connection,Action<bool,string> check)
 {
  var services=new ServiceCollection();services.AddLogging();services.AddDataProtection().UseEphemeralDataProtectionProvider();
  services.AddDbContext<HrDbContext>(o=>o.UseMySql(connection,new MySqlServerVersion(new Version(8,0,0))));
  services.AddIdentityCore<ApplicationUser>(o=>o.Password.RequiredLength=10).AddRoles<IdentityRole>().AddEntityFrameworkStores<HrDbContext>().AddDefaultTokenProviders();
  services.Configure<DataProtectionTokenProviderOptions>(o=>o.TokenLifespan=TimeSpan.FromHours(1));
  using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var db=scope.ServiceProvider.GetRequiredService<HrDbContext>();
  var email="reset-"+Guid.NewGuid().ToString("N")+"@test.local";var user=new ApplicationUser{UserName=email,Email=email,MustChangePassword=true};
  check((await users.CreateAsync(user,"Fixture!OldPassword123")).Succeeded,"recovery fixture user created");
  var sender=new CapturingResetDelivery();var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"PasswordReset:PublicOrigin","http://127.0.0.1:5189"}}).Build();
  var recovery=new PasswordRecoveryService(users,db,sender,config,new TestWebEnvironment(),NullLogger<PasswordRecoveryService>.Instance);
  await recovery.RequestAsync("missing-"+email);check(sender.Messages.Count==0,"unknown emails do not create reset deliveries");
  await recovery.RequestAsync(email);check(sender.Messages.Count==1,"eligible account receives a reset link");
  var code=QueryHelpers.ParseQuery(new Uri(sender.Messages[0]).Query)["code"].ToString();var stamp=user.SecurityStamp;
  var invalid=await recovery.ResetAsync(email,"malformed!","Fixture!NewPassword456");check(!invalid.Succeeded,"malformed reset tokens are rejected");
  var result=await recovery.ResetAsync(email,code,"Fixture!NewPassword456");check(result.Succeeded,"valid reset token changes the password");
  check(await users.CheckPasswordAsync(user,"Fixture!NewPassword456")&&!await users.CheckPasswordAsync(user,"Fixture!OldPassword123"),"new password works and old password stops working");
  check(!user.MustChangePassword&&user.SecurityStamp!=stamp,"password recovery clears first-login flag and invalidates old sessions");
  check(!(await recovery.ResetAsync(email,code,"Fixture!OtherPassword789")).Succeeded,"reset token cannot be replayed");db.ChangeTracker.Clear();user=(await users.FindByEmailAsync(email))!;
  var expiredToken=await users.GeneratePasswordResetTokenAsync(user);scope.ServiceProvider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan=TimeSpan.FromTicks(-1);
  check(!await users.VerifyUserTokenAsync(user,users.Options.Tokens.PasswordResetTokenProvider,UserManager<ApplicationUser>.ResetPasswordTokenPurpose,expiredToken),"expired password reset tokens are rejected");
  scope.ServiceProvider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan=TimeSpan.FromHours(1);user.IsEnabled=false;await users.UpdateAsync(user);await recovery.RequestAsync(email);check(sender.Messages.Count==1,"disabled accounts do not receive reset deliveries");
  var http=new Microsoft.AspNetCore.Http.DefaultHttpContext();
  http.User=new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier,user.Id),new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name,"test-admin")},"Test"));
  var page=new HrSystemDhg.Web.Pages.Admin.UsersModel(db,users){PageContext=new Microsoft.AspNetCore.Mvc.RazorPages.PageContext{HttpContext=http}};
  check(await page.OnPostRevokeAsync(user.Id) is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,"cannot revoke own administrator access");
  check(await page.OnPostToggleAsync(user.Id) is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,"cannot disable own account");
  check(await page.OnPostRoleAsync(user.Id,"Admin",true) is Microsoft.AspNetCore.Mvc.BadRequestResult,"cannot remove own Admin role");
  check(await page.OnPostRoleAsync(user.Id,"UnrecognizedRole",false) is Microsoft.AspNetCore.Mvc.BadRequestResult,"unrecognized role grant rejected");
  var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
  if(!await roles.RoleExistsAsync("Admin"))await roles.CreateAsync(new IdentityRole("Admin"));
  var other=new ApplicationUser{UserName="permissions-"+Guid.NewGuid().ToString("N")+"@test.local"};await users.CreateAsync(other);await users.AddToRoleAsync(other,"Admin");var previousStamp=other.SecurityStamp;
  page.TempData=new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(http,new TestTempDataProvider());
  await page.OnPostRevokeAsync(other.Id);
  check((await users.GetRolesAsync(other)).Count==0 && previousStamp!=other.SecurityStamp,"revoking all roles removes grants and invalidates sessions");

  var branch = new Branch { Name="Registration fixture", Code=Guid.NewGuid().ToString("N"), IsActive=true };
  db.Branchs.Add(branch);await db.SaveChangesAsync();
  var department=new Department{Name="Registration fixture",BranchId=branch.Id,IsActive=true};db.Departments.Add(department);await db.SaveChangesAsync();
  if(!await roles.RoleExistsAsync("Employee"))await roles.CreateAsync(new IdentityRole("Employee"));
  var registration = new HrSystemDhg.Web.Pages.Hr.RegisterModel(db) { Number="REG-"+Guid.NewGuid().ToString("N"), Name="Synthetic Registration", Email="reg-"+Guid.NewGuid().ToString("N")+"@test.local",Hire=BusinessClock.Today.AddDays(-1),BranchId=branch.Id,DepartmentId=department.Id,Title="Test role",InitialPassword="Fixture!Password123",PageContext=new Microsoft.AspNetCore.Mvc.RazorPages.PageContext{HttpContext=http}};
  var created=await registration.OnPostAsync(users);
  check(created is Microsoft.AspNetCore.Mvc.RedirectToPageResult,"valid registration creates an employee and advances to onboarding");
  var employee=await db.Employees.SingleAsync(e=>e.EmployeeNumber==registration.Number);
  check(await db.EmployeeAssignments.AnyAsync(a=>a.EmployeeId==employee.Id) && (await users.FindByEmailAsync(registration.Email))?.EmployeeId==employee.Id,"registration saves employee assignment and linked login atomically");
  var onboarding=new HrSystemDhg.Web.Pages.Hr.OnboardingModel(db,new CalendarService(db),new DocumentService(db,new TestWebEnvironment())){EmployeeId=employee.Id,Step=2,Gender=null!,Nationality=null!,Phone=null!,PersonalEmail=null!,Address=null!,Marital=null!,Emergency=null!,Relation=null!,EmergencyPhone=null!,PageContext=new Microsoft.AspNetCore.Mvc.RazorPages.PageContext{HttpContext=http}};
  check(await onboarding.OnPostAsync(false) is Microsoft.AspNetCore.Mvc.RedirectToPageResult,"blank optional personal fields save without database null errors");
  onboarding.Step=3;onboarding.Institution="Unsaved";
  check(await onboarding.OnPostSkipAsync() is Microsoft.AspNetCore.Mvc.RedirectToPageResult && !await db.Educations.AnyAsync(e=>e.EmployeeId==employee.Id),"skip education advances without saving incomplete input");
  onboarding.Step=2;onboarding.Marital="Invalid status";
  check(await onboarding.OnPostAsync(false) is Microsoft.AspNetCore.Mvc.RazorPages.PageResult && !onboarding.ModelState.IsValid,"invalid marital selection is rejected");
  onboarding.ModelState.Clear();onboarding.Marital="Single";onboarding.Nationality="Somalia";onboarding.Birth=BusinessClock.Today.AddDays(1);
  check(await onboarding.OnPostAsync(false) is Microsoft.AspNetCore.Mvc.RazorPages.PageResult && !onboarding.ModelState.IsValid,"future birth date is rejected");
  onboarding.ModelState.Clear();onboarding.Step=6;onboarding.TempData=new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(http,new TestTempDataProvider());
  check(await onboarding.OnPostAsync(false) is Microsoft.AspNetCore.Mvc.RedirectToPageResult && !employee.RegistrationComplete,"finishing without salary saves employee with payroll pending");

 }
 private sealed class TestTempDataProvider:Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider{public IDictionary<string,object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context)=>new Dictionary<string,object>();public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context,IDictionary<string,object> values){}}
 private sealed class CapturingResetDelivery:IPasswordResetDelivery{public List<string> Messages=[];public Task SendAsync(string email,string link){Messages.Add(link);return Task.CompletedTask;}}
 private sealed class TestWebEnvironment:IWebHostEnvironment{public string ApplicationName{get;set;}="DHG.Tests";public string EnvironmentName{get;set;}="Development";public string WebRootPath{get;set;}="";public string ContentRootPath{get;set;}=Path.GetTempPath();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
