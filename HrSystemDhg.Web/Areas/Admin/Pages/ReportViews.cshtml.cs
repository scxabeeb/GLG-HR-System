using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class ReportViewsModel(HrDbContext db) : PageModel
{
    [BindProperty] public SavedReportView Input { get; set; } = new();
    [BindProperty] public int[] SelectedColumns { get; set; } = [];
    [BindProperty(SupportsGet = true)] public string Source { get; set; } = "Employees";
    public List<SavedReportView> Items { get; set; } = [];
    public List<Branch> Branches { get; set; } = [];
    public List<Department> Departments { get; set; } = [];
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id.HasValue)
        {
            var saved = await db.SavedReportViews.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id);
            if (saved == null) return NotFound();
            Input = saved; Source = saved.Source; SelectedColumns = ReportDefinitions.ColumnIndexes(saved.Columns);
        }
        else
        {
            var source = Source ?? "";
            if (!ReportDefinitions.Sources.ContainsKey(source)) return BadRequest();
            Source = source; Input.Source = source; SelectedColumns = Enumerable.Range(0, ReportDefinitions.Sources[source].Length).ToArray();
        }
        await Load(); return Page();
    }
    async Task Load()
    {
        Items = await db.SavedReportViews.AsNoTracking().OrderBy(v => v.Name).ToListAsync();
        Branches = await db.Branchs.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
        Departments = await db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        var source = Input.Source ?? "";
        if (!ReportDefinitions.Sources.TryGetValue(source, out var columns)) return BadRequest();
        Source = source;
        SelectedColumns = SelectedColumns.Distinct().Order().ToArray();
        if (string.IsNullOrWhiteSpace(Input.Name) || Input.Name.Length > 160 || Input.Description?.Length > 500 || Input.Status?.Length > 80 ||
            Input.DateRange is not ("ThisMonth" or "LastMonth" or "ThisYear") || SelectedColumns.Length == 0 || SelectedColumns.Any(c => c < 0 || c >= columns.Length))
            ModelState.AddModelError("", "Enter a report name, valid date range and at least one available column.");
        if (Input.BranchId != null && !await db.Branchs.AnyAsync(b => b.Id == Input.BranchId) || Input.DepartmentId != null && !await db.Departments.AnyAsync(d => d.Id == Input.DepartmentId && (Input.BranchId == null || (d.BranchId == Input.BranchId || d.AdditionalBranches.Any(b => b.BranchId == Input.BranchId)))))
            ModelState.AddModelError("", "Choose a valid branch and department belonging to that branch.");
        if (await db.SavedReportViews.AnyAsync(v => v.Name == (Input.Name ?? "").Trim() && v.Id != Input.Id)) ModelState.AddModelError("", "A report with this name already exists.");
        if (!ModelState.IsValid) { await Load(); return Page(); }
        var row = Input.Id == 0 ? new SavedReportView() : await db.SavedReportViews.FindAsync(Input.Id);
        if (row == null) return NotFound();
        row.Name = Input.Name.Trim(); row.Description = Input.Description?.Trim() ?? ""; row.Source = source;
        row.Columns = string.Join(',', SelectedColumns); row.DateRange = Input.DateRange; row.BranchId = Input.BranchId; row.DepartmentId = Input.DepartmentId;
        row.Status = string.IsNullOrWhiteSpace(Input.Status) ? null : Input.Status.Trim(); row.IsActive = Input.IsActive;
        if (row.Id == 0) db.SavedReportViews.Add(row);
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Saved report definition: {row.Name}" });
        await db.SaveChangesAsync(); TempData["Notice"] = "Report definition saved.";
        return RedirectToPage(null, null, "report-library");
    }
    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var row = await db.SavedReportViews.FindAsync(id); if (row == null) return NotFound();
        row.IsActive = !row.IsActive;
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Report {row.Name}: active={row.IsActive}" });
        await db.SaveChangesAsync(); return RedirectToPage(null, null, "report-library");
    }
}
