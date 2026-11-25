using HrSystemDhg.Web.Services;
using HrSystemDhg.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.WebUtilities;

namespace HrSystemDhg.Web.TagHelpers;

// Runs after MVC generates href values, so all navigation uses the server permission catalog.
[HtmlTargetElement("a")]
public sealed class MenuAccessTagHelper(ModuleAccess access, HrDbContext db) : TagHelper
{
    [HtmlAttributeName("menu-permission")] public string? Permission { get; set; }
    public override int Order => 1000;
    [ViewContext, HtmlAttributeNotBound] public ViewContext ViewContext { get; set; } = null!;
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewContext.HttpContext.User.Identity?.IsAuthenticated != true) return;
        var href = output.Attributes["href"]?.Value?.ToString();
        if (Permission != null && !MenuPermissions.Allows(await access.BlockedAsync(), Permission)) { output.SuppressOutput(); return; }
        if (string.IsNullOrEmpty(href) || !href.StartsWith('/') || href.StartsWith("//")) return;
        var pieces = href.Split('?',2);
        var query = new QueryCollection(QueryHelpers.ParseQuery(pieces.Length == 2 ? pieces[1].Split('#')[0] : ""));
        var path = pieces[0].Split('#')[0];
        var menu = MenuPermissions.ForUrl(path,query);
        if (path.TrimEnd('/').ToLowerInvariant() is "/reports" or "/reports/index" && int.TryParse(query["SavedViewId"].FirstOrDefault(), out var savedId))
        {
            var source = await db.SavedReportViews.AsNoTracking().Where(v=>v.Id==savedId && v.IsActive).Select(v=>v.Source).SingleOrDefaultAsync();
            if (source != null) menu = MenuPermissions.Report(source);
        }
        if (!MenuPermissions.Allows(await access.BlockedAsync(), menu)) output.SuppressOutput();
    }
}
