using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

if (EF.IsDesignTime)
    return;
var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("App_Data/local-settings.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();
}

var cs = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost;Database=hr_system_dhg;User=hr_app;Password=;";
builder.Services.AddDbContext<HrDbContext>(o => o.UseMySql(cs, new MySqlServerVersion(new Version(8, 0, 0))));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 10;
    o.Lockout.MaxFailedAccessAttempts = 5;
}).AddEntityFrameworkStores<HrDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Identity/Account/Login";
    o.AccessDeniedPath = "/Denied";
    o.SlidingExpiration = true;
});
builder.Services.Configure<SecurityStampValidatorOptions>(o =>
{
    o.ValidationInterval = TimeSpan.FromMinutes(1);
    o.OnRefreshingPrincipal = c =>
    {
        foreach (var claim in c.CurrentPrincipal?.Claims.Where(x => x.Type.StartsWith("support-")) ?? [])
            c.NewPrincipal?.Identities.First().AddClaim(claim);
        return Task.CompletedTask;
    };
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 12 * 1024 * 1024;
    o.ValueLengthLimit = 16000;
});
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/");
    o.Conventions.AuthorizeAreaFolder("Admin", "/");
    o.Conventions.AuthorizeAreaFolder("Hr", "/");
    o.Conventions.AuthorizeAreaFolder("Management", "/");
    o.Conventions.AuthorizeAreaFolder("Identity", "/");
    o.Conventions.AllowAnonymousToPage("/Error");
    o.Conventions.AllowAnonymousToPage("/Denied");
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<DataProtectionTokenProviderOptions>(o=>o.TokenLifespan=TimeSpan.FromHours(1));
builder.Services.AddScoped<IPasswordResetDelivery,PasswordResetDelivery>();
builder.Services.AddScoped<PasswordRecoveryService>();
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("password-reset",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(15),QueueLimit=0,AutoReplenishment=true}));});
builder.Services.AddScoped<CalendarService>();
builder.Services.AddScoped<LeaveService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<PayrollService>();
builder.Services.AddScoped<AttendanceService>();
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (builder.Configuration.GetValue<bool>("Security:RequireHttps"))
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var users = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var signIn = context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
        var user = await users.GetUserAsync(context.User);
        if (user == null || !user.IsEnabled || SupportSession.Expired(context.User, DateTimeOffset.UtcNow))
        {
            await signIn.SignOutAsync();
            context.Response.Redirect("/Identity/Account/Login");
            return;
        }

        var originalId = context.User.FindFirst("support-admin")?.Value;
        if (originalId != null)
        {
            var original = await users.FindByIdAsync(originalId);
            if (original == null || !original.IsEnabled || !await users.IsInRoleAsync(original, "Admin"))
            {
                await signIn.SignOutAsync();
                context.Response.Redirect("/Identity/Account/Login");
                return;
            }

            if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path != "/Identity/Account/EndSupport")
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Support sessions are read-only. Return to Admin to make changes.");
                return;
            }
        }
        else if (user.MustChangePassword && !context.Request.Path.StartsWithSegments("/Identity/Account"))
        {
            context.Response.Redirect("/Identity/Account/ChangePassword");
            return;
        }
    }

    await next();
});
app.MapRazorPages();
if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<HrDbContext>().Database.MigrateAsync();
    return;
}

if (args.Contains("--seed"))
{
    await StartupSeed.RunAsync(app.Services, builder.Configuration);
    return;
}

await StartupSeed.RunAsync(app.Services, builder.Configuration);
if (args.Contains("--demo"))
{
    await DemoSeed.RunAsync(app.Services, builder.Configuration, app.Environment);
    return;
}

app.Run();
