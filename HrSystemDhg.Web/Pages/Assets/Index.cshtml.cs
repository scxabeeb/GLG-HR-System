using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HrSystemDhg.Web.Pages.Assets;

[Authorize(Roles = "Admin,Management,HR")]
public class IndexModel(HrDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public bool Report { get; set; }
    [BindProperty(SupportsGet = true), StringLength(150)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int? BranchId { get; set; }
    [BindProperty(SupportsGet = true)] public int? DepartmentId { get; set; }
    [BindProperty(SupportsGet = true)] public int? EmployeeId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }
    [BindProperty(SupportsGet = true)] public string? Custody { get; set; }
    [BindProperty(SupportsGet = true)] public AssetStatus? Status { get; set; }

    public List<SelectListItem> Branches { get; private set; } = [];
    public List<SelectListItem> Departments { get; private set; } = [];
    public List<SelectListItem> Employees { get; private set; } = [];
    public List<SelectListItem> Categories { get; private set; } = [];
    public List<AssetRow> Rows { get; private set; } = [];
    public record AssetRow(Asset Asset, string Branch, string? Department, string? Employee, bool HasPhoto);

    public async Task<IActionResult> OnGetAsync(string? format)
    {
        Branches = await db.Branchs.OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        Departments = await db.Departments.OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        Employees = await db.Employees.OrderBy(x => x.FullName).Select(x => new SelectListItem(x.FullName + " · " + x.EmployeeNumber, x.Id.ToString())).ToListAsync();
        Categories = await db.Assets.Select(x => x.Category).Distinct().OrderBy(x => x)
            .Select(x => new SelectListItem(x, x)).ToListAsync();

        if (!ModelState.IsValid)
            return format == null ? Page() : BadRequest();
        var search = Search?.Trim();
        var query = db.Assets.AsNoTracking().Where(x =>
            (BranchId == null || x.BranchId == BranchId) &&
            (DepartmentId == null || x.DepartmentId == DepartmentId) &&
            (EmployeeId == null || x.EmployeeId == EmployeeId) &&
            (Category == null || x.Category == Category) &&
            (Status == null || x.Status == Status));
        if (Custody == "Office")
            query = query.Where(x => x.EmployeeId == null);
        if (Custody == "Employee")
            query = query.Where(x => x.EmployeeId != null);
        if (Custody == "Both")
            query = query.Where(x => x.EmployeeId != null &&
                (x.DepartmentId != null || (x.OfficeLocation != null && x.OfficeLocation.Trim() != "")));
        if (!string.IsNullOrEmpty(search))
            query = query.Where(x => x.Tag.Contains(search) || x.Name.Contains(search) ||
                (x.SerialNumber != null && x.SerialNumber.Contains(search)));

        Rows = await query.OrderBy(x => x.Tag).Select(x => new AssetRow(x,
            db.Branchs.Where(b => b.Id == x.BranchId).Select(b => b.Name).FirstOrDefault() ?? "",
            db.Departments.Where(d => d.Id == x.DepartmentId).Select(d => d.Name).FirstOrDefault(),
            db.Employees.Where(e => e.Id == x.EmployeeId).Select(e => e.FullName).FirstOrDefault(),
            x.Photo != null)).ToListAsync();

        if (format == null)
            return Page();
        if (!Report || format is not ("csv" or "excel" or "pdf"))
            return BadRequest();

        List<string[]> export = [["Asset tag", "Name", "Category", "Brand", "Model", "Serial number",
            "Branch", "Department", "Office location", "Assignment", "Employee", "Status", "Condition",
            "Supplier", "Purchase reference", "Purchase date", "Warranty until", "Currency", "Purchase cost", "Book value", "Notes"]];
        foreach (var row in Rows)
        {
            var x = row.Asset;
            export.Add([x.Tag, x.Name, x.Category, x.Brand ?? "", x.Model ?? "", x.SerialNumber ?? "",
                row.Branch, row.Department ?? "", x.OfficeLocation ?? "", Assignment(x),
                row.Employee ?? "", Label(x.Status), x.Condition ?? "", x.Supplier ?? "", x.PurchaseReference ?? "",
                x.PurchasedOn?.ToString("yyyy-MM-dd") ?? "", x.WarrantyUntil?.ToString("yyyy-MM-dd") ?? "", x.Currency,
                x.PurchaseCost?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
                x.BookValue?.ToString("F2", CultureInfo.InvariantCulture) ?? "", x.Notes ?? ""]);
        }
        return ExportService.Report(export, "assets", format);
    }

    public static string Assignment(Asset asset)
    {
        if (asset.EmployeeId == null)
            return "Office only";
        return asset.DepartmentId != null || !string.IsNullOrWhiteSpace(asset.OfficeLocation)
            ? "Office and employee"
            : "Employee only";
    }

    public static string Label(AssetStatus status) => status switch
    {
        AssetStatus.InUse => "In use",
        AssetStatus.UnderRepair => "Under repair",
        _ => status.ToString()
    };
}
