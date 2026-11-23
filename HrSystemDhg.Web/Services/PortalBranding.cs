using HrSystemDhg.Web.Data;
using Microsoft.EntityFrameworkCore;
namespace HrSystemDhg.Web.Services;
public sealed class PortalBranding(HrDbContext db)
{
    public sealed record Brand(string Name, string ShortName, string Subtitle, string Organization, string LogoUrl);
    private Brand? value;
    public async Task<Brand> GetAsync()
    {
        if (value != null) return value;
        var s = await db.Settings.AsNoTracking().Select(s => new { s.PortalName, s.ShortName, s.PortalSubtitle, s.CompanyName, HasLogo = s.Logo != null, s.LogoUpdatedUtc }).FirstOrDefaultAsync();
        return value = s == null ? new("Garowe City Management System", "Garowe City", "Management System", "Garowe Local Government", "/images/garowe-local-government.jpg") :
            new(s.PortalName, s.ShortName, s.PortalSubtitle, s.CompanyName, s.HasLogo ? "/branding/logo?v=" + s.LogoUpdatedUtc?.Ticks : "/images/garowe-local-government.jpg");
    }
    public static void Map(WebApplication app) => app.MapGet("/branding/logo", async (HrDbContext db, HttpContext context) => {
        var logo = await db.Settings.AsNoTracking().Select(s => new { s.Logo, s.LogoContentType }).FirstOrDefaultAsync();
        if (logo?.Logo == null) return Results.Redirect("/images/garowe-local-government.jpg");
        context.Response.Headers.CacheControl = "public,max-age=3600";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.File(logo.Logo, logo.LogoContentType == "image/png" ? "image/png" : "image/jpeg");
    }).AllowAnonymous();
}
