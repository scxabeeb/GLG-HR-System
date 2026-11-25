using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

if (EF.IsDesignTime)
    return;
var builder = WebApplication.CreateBuilder(args);
ReportPdf.ConfigureFonts();
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("App_Data/local-settings.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();
}

var cs = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost;Database=hr_system_dhg;User=hr_app;Password=;";
builder.Services.AddDbContext<HrDbContext>(o => o.UseMySql(cs, new MySqlServerVersion(new Version(8, 0, 0))));
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 10;
    o.Lockout.MaxFailedAccessAttempts = 5;
}).AddEntityFrameworkStores<HrDbContext>().AddDefaultTokenProviders();
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, SafePasswordHasher>();
builder.Services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, RoleHierarchyClaimsTransformation>();
builder.Services.AddAuthentication().AddBearerToken(NativeMobileApi.Scheme, o =>
{
    o.BearerTokenExpiration = TimeSpan.FromHours(8);
});
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Identity/Account/Login";
    o.AccessDeniedPath = "/Denied";
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { if (c.Request.Path.StartsWithSegments("/api/mobile")) c.Response.StatusCode = 401; else c.Response.Redirect(c.RedirectUri); return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { if (c.Request.Path.StartsWithSegments("/api/mobile")) c.Response.StatusCode = 403; else c.Response.Redirect(c.RedirectUri); return Task.CompletedTask; };
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
builder.Services.AddScoped<OperationsNotices>();
builder.Services.AddScoped<ModuleAccess>();
builder.Services.AddScoped<PortalBranding>();
builder.Services.AddHttpClient("whatsapp", client => client.Timeout = TimeSpan.FromSeconds(12))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHostedService<WhatsAppWorker>();
builder.Services.AddHostedService<AttendanceDeadlineWorker>();
builder.Services.AddRateLimiter(options => options.AddPolicy("fleet-gps", context =>
    RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 180, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
builder.Services.AddSingleton<RoomLinks>();
builder.Services.AddSingleton<MeetingRooms>();
builder.Services.AddRateLimiter(options => options.AddPolicy("meeting-join", context =>
    RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
builder.Services.AddRateLimiter(options => options.AddPolicy("attendance-push", context =>
    RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

var pushPort = builder.Configuration.GetValue<int?>("AttendancePush:ListenerPort");
if (pushPort is > 0 and <= 65535)
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value;
        var isDeviceRoute = path != null && new[] { "/iclock/cdata", "/iclock/cdata.aspx", "/iclock/getrequest", "/iclock/getrequest.aspx", "/iclock/devicecmd", "/iclock/devicecmd.aspx" }
            .Contains(path, StringComparer.OrdinalIgnoreCase);
        if ((context.Connection.LocalPort == pushPort) != isDeviceRoute)
        {
            context.Response.StatusCode = 404;
            return;
        }
        await next();
    });
}

if (builder.Configuration.GetValue<bool>("Security:RequireHttps"))
    app.UseWhen(context => context.Connection.LocalPort != pushPort, branch => branch.UseHttpsRedirection());
app.UseStaticFiles();
app.UseWebSockets();
app.UseRouting();
app.UseRateLimiter();
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api/mobile")) ctx.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var users = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var signIn = context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
        var user = await users.GetUserAsync(context.User);
        var native = context.Request.Path.StartsWithSegments("/api/mobile");
        var bearer = context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        var invalidStamp = bearer && user != null && context.User.FindFirst("AspNet.Identity.SecurityStamp")?.Value != user.SecurityStamp;
        if (user == null || !user.IsEnabled || invalidStamp || SupportSession.Expired(context.User, DateTimeOffset.UtcNow))
        {
            if (native) { context.Response.StatusCode = 401; return; }
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
        else if (user.MustChangePassword && !context.Request.Path.StartsWithSegments("/Identity/Account")
            && !context.Request.Path.StartsWithSegments("/api/mobile/v2/auth"))
        {
            if (native)
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { message = "Change your temporary password to continue.", code = "password_change_required" });
                return;
            }
            var returnUrl = context.Request.PathBase.Add(context.Request.Path).ToUriComponent() + context.Request.QueryString.ToUriComponent();
            context.Response.Redirect(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
                "/Identity/Account/ChangePassword", "ReturnUrl", returnUrl));
            return;
        }
    }

    if (context.User.Identity?.IsAuthenticated == true)
    {
        var area = context.GetRouteValue("area")?.ToString();
        var page = context.GetRouteValue("page")?.ToString();
        var module = ModuleAccess.ForRoute(area, page);
        var denied = await context.RequestServices.GetRequiredService<ModuleAccess>().BlockedAsync();
        var menu = MenuPermissions.ForUrl(context.Request.Path.Value ?? "", context.Request.Query, context.Request.Method);
        // A saved report determines its source on the server; query parameters cannot override its permission.
        if (page == "/Reports/Index" && int.TryParse(context.Request.Query["SavedViewId"].FirstOrDefault(), out var savedId))
        {
            var source = await context.RequestServices.GetRequiredService<HrDbContext>().SavedReportViews.AsNoTracking()
                .Where(v => v.Id == savedId && v.IsActive).Select(v => v.Source).SingleOrDefaultAsync();
            if (source != null) menu = MenuPermissions.Report(source);
        }
        var extraTaskPermission = page == "/Operations/MeetingDetails" && HttpMethods.IsPost(context.Request.Method) && string.Equals(context.Request.Query["handler"].FirstOrDefault(), "FollowUp", StringComparison.OrdinalIgnoreCase);
        if (!ModuleAccess.Allows(denied, module) || !MenuPermissions.Allows(denied, menu) || (extraTaskPermission && !MenuPermissions.Allows(denied, "tasks.manage")))
        {
            if (module == "overview" && HttpMethods.IsGet(context.Request.Method))
                context.Response.Redirect("/Operations/Notifications");
            else
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                if (context.Request.Path.StartsWithSegments("/api"))
                    await context.Response.WriteAsJsonAsync(new { message = "Your administrator has denied access to this menu or action.", code = "menu_access_denied" });
                else
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync("""
                        <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Access denied</title></head>
                        <body style="margin:0;background:#f3f6fb;color:#183153;font:16px system-ui,sans-serif"><main style="max-width:520px;margin:12vh auto;padding:32px;background:white;border-radius:12px;border:1px solid #dbe3ef">
                        <p style="color:#1749bd;font-size:13px">WORKSPACE ACCESS</p><h1>Access denied</h1><p>Your account does not have permission to open this page or perform this action. Ask your administrator to review your menu permissions.</p>
                        <a href="/Operations/Notifications" style="display:inline-block;background:#1749bd;color:white;padding:12px 18px;border-radius:6px;text-decoration:none">Return to workspace</a></main></body></html>
                        """);
                }
            }
            return;
        }
    }

    await next();
});
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == StatusCodes.Status404NotFound)
        app.Logger.LogWarning("Page not found: {Method} {Path}", context.Request.Method, context.Request.Path);
});
PortalBranding.Map(app);
AttendanceOfficeBridge.Map(app);
app.MapRazorPages();
app.MapGet("/api/mobile/v1/{section}", MobileEmployeeApi.Read).RequireAuthorization(NativeMobileApi.LegacyPolicy);
NativeMobileApi.Map(app);
NativeOperationsManagement.Map(app);
foreach (var path in new[] { "/iclock/cdata", "/iclock/cdata.aspx" })
{
    app.MapGet(path, ZkAttendancePush.Poll).AllowAnonymous().RequireRateLimiting("attendance-push");
    app.MapPost(path, ZkAttendancePush.Upload).AllowAnonymous().RequireRateLimiting("attendance-push");
}
foreach (var path in new[] { "/iclock/getrequest", "/iclock/getrequest.aspx" })
    app.MapGet(path, ZkAttendancePush.NextCommand).AllowAnonymous().RequireRateLimiting("attendance-push");
foreach (var path in new[] { "/iclock/devicecmd", "/iclock/devicecmd.aspx" })
    app.MapPost(path, ZkAttendancePush.CommandResult).AllowAnonymous().RequireRateLimiting("attendance-push");

app.MapGet("/operations/meeting-signal", (HttpContext context, MeetingRooms rooms, HrDbContext db, RoomLinks links) =>
    rooms.Handle(context, db, links)).AllowAnonymous().RequireRateLimiting("meeting-join");
app.MapPost("/api/fleet/{vehicleId:int}/positions", FleetTelemetry.Receive)
    .AllowAnonymous().RequireRateLimiting("fleet-gps");
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
