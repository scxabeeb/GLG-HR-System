using System.ComponentModel.DataAnnotations;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace HrSystemDhg.Web.Pages.Assets;

[Authorize(Roles = "Admin,Management,HR")]
[RequestSizeLimit(6 * 1024 * 1024)]
public class EditModel(HrDbContext db) : PageModel
{
    public const int MaxPhotoSize = 5 * 1024 * 1024;
    [BindProperty] public Asset Input { get; set; } = new();
    [BindProperty] public IFormFile? PhotoUpload { get; set; }
    [BindProperty] public bool RemovePhoto { get; set; }
    [BindProperty, StringLength(500)] public string? ChangeNote { get; set; }

    public int? AssetId { get; private set; }
    public bool HasPhoto { get; private set; }
    public List<SelectListItem> Branches { get; private set; } = [];
    public List<SelectListItem> Departments { get; private set; } = [];
    public List<SelectListItem> Employees { get; private set; } = [];
    public List<HistoryRow> History { get; private set; } = [];
    public record HistoryRow(AssetActivity Entry, string? Employee, string? Branch);

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id != null)
        {
            var asset = await db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (asset == null)
                return NotFound();
            Input = asset;
        }
        await LoadPageAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnGetPhotoAsync(int id)
    {
        var photo = await db.AssetPhotos.AsNoTracking()
            .SingleOrDefaultAsync(x => x.AssetId == id && db.Assets.Any(a => a.Id == x.AssetId));
        if (photo == null)
            return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(photo.Content, photo.ContentType);
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        var asset = id == null ? null : await db.Assets.SingleOrDefaultAsync(x => x.Id == id);
        if (id != null && asset == null)
            return NotFound();

        if (asset != null && asset.Version != Input.Version)
            ModelState.AddModelError("", "This asset changed after you opened it. Reload the record before saving again.");

        if (!await db.Branchs.AnyAsync(x => x.Id == Input.BranchId))
            ModelState.AddModelError("Input.BranchId", "Select an authorized branch.");
        if (Input.DepartmentId != null && !await db.Departments.AnyAsync(x => x.Id == Input.DepartmentId && x.BranchId == Input.BranchId))
            ModelState.AddModelError("Input.DepartmentId", "Select a department in the asset’s branch.");
        if (Input.EmployeeId != null && Input.EmployeeId != asset?.EmployeeId)
        {
            var today = BusinessClock.Today;
            if (!await db.Employees.AnyAsync(x => x.Id == Input.EmployeeId && x.Status == "Active" &&
                db.EmployeeAssignments.Any(a => a.EmployeeId == x.Id && a.BranchId == Input.BranchId &&
                    a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))))
                ModelState.AddModelError("Input.EmployeeId", "Select an active employee assigned to the asset’s branch.");
        }
        if (asset?.EmployeeId != null && asset.EmployeeId == Input.EmployeeId && asset.BranchId != Input.BranchId)
            ModelState.AddModelError("Input.EmployeeId", "Select a new custodian or return the asset to office custody before changing branches.");
        if (Input.WarrantyUntil < Input.PurchasedOn)
            ModelState.AddModelError("Input.WarrantyUntil", "Warranty expiry must be on or after the purchase date.");
        if (RemovePhoto && PhotoUpload != null)
            ModelState.AddModelError(nameof(PhotoUpload), "Choose either to replace or remove the existing photo.");

        var newPhoto = PhotoUpload == null ? null : await ReadPhotoAsync(PhotoUpload);
        if (!ModelState.IsValid)
        {
            await LoadPageAsync(id);
            return Page();
        }

        Input.Id = asset?.Id ?? 0;
        Input.Version = Guid.NewGuid();
        Input.Tag = Input.Tag.Trim().ToUpperInvariant();
        Input.Name = Input.Name.Trim();
        Input.Category = Input.Category.Trim();
        Input.Currency = Input.Currency.ToUpperInvariant();
        Input.Photo = null;

        var action = asset == null ? "Registered" :
            asset.EmployeeId != Input.EmployeeId || asset.BranchId != Input.BranchId ||
            asset.DepartmentId != Input.DepartmentId || asset.OfficeLocation != Input.OfficeLocation ? "Custody updated" :
            asset.Status != Input.Status ? "Status updated" : "Details updated";

        var previousEmployee = asset?.EmployeeId;
        if (asset == null)
        {
            asset = Input;
            db.Assets.Add(asset);
        }
        else
        {
            db.Entry(asset).CurrentValues.SetValues(Input);
        }

        if (RemovePhoto || newPhoto != null)
        {
            var previous = id == null ? null : await db.AssetPhotos.FindAsync(id.Value);
            if (previous != null && newPhoto != null)
            {
                previous.ContentType = newPhoto.ContentType;
                previous.Content = newPhoto.Content;
            }
            else if (previous != null)
                db.AssetPhotos.Remove(previous);
            else if (newPhoto != null)
                asset.Photo = newPhoto;
        }

        db.Add(new AssetActivity
        {
            Asset = asset, BranchId = asset.BranchId, EmployeeId = asset.EmployeeId, OfficeLocation = asset.OfficeLocation,
            RecordedUtc = DateTime.UtcNow, Actor = User.Identity?.Name ?? "", Action = action,
            Status = asset.Status, BookValue = asset.BookValue, Currency = asset.Currency, Note = ChangeNote
        });

        try
        {
            if (asset.EmployeeId is int employeeId && employeeId != previousEmployee)
                await OperationsNotices.AddEmployeeUpdatesAsync(db, [employeeId], OperationsNoticeKind.AssetAssigned,
                    $"{asset.Tag} · {asset.Name}");
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", "This asset changed while you were editing it. Reload the record before saving again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            ModelState.AddModelError("Input.Tag", "That asset tag is already registered.");
        }

        if (!ModelState.IsValid)
        {
            await LoadPageAsync(id);
            return Page();
        }

        TempData["AssetMessage"] = action == "Registered" ? "Asset registered." : "Asset updated.";
        return RedirectToPage("./Edit", new { id = asset.Id });
    }

    private async Task<AssetPhoto?> ReadPhotoAsync(IFormFile upload)
    {
        if (upload.Length is > 0 and <= MaxPhotoSize)
        {
            using var buffer = new MemoryStream((int)upload.Length);
            await upload.CopyToAsync(buffer, HttpContext.RequestAborted);
            var bytes = buffer.ToArray();
            var type = DetectImage(bytes);
            if (type != null)
                return new AssetPhoto { ContentType = type, Content = bytes };
        }

        ModelState.AddModelError(nameof(PhotoUpload), "Choose a JPEG, PNG or WebP photo up to 5 MB.");
        return null;
    }

    private static string? DetectImage(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
            return "image/jpeg";
        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }

    private async Task LoadPageAsync(int? id)
    {
        AssetId = id;
        Branches = await db.Branchs.OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        Departments = await (from department in db.Departments join branch in db.Branchs on department.BranchId equals branch.Id
            orderby branch.Name, department.Name
            select new SelectListItem(branch.Name + " / " + department.Name, department.Id.ToString())).ToListAsync();
        Employees = await db.Employees.Where(x => x.Status == "Active" || x.Id == Input.EmployeeId).OrderBy(x => x.FullName)
            .Select(x => new SelectListItem(x.FullName + " · " + x.EmployeeNumber, x.Id.ToString())).ToListAsync();
        if (id == null)
            return;
        HasPhoto = await db.AssetPhotos.AnyAsync(x => x.AssetId == id);
        History = await db.AssetActivities.AsNoTracking().Where(x => x.AssetId == id)
            .OrderByDescending(x => x.RecordedUtc).ThenByDescending(x => x.Id)
            .Select(x => new HistoryRow(x, db.Employees.Where(e => e.Id == x.EmployeeId).Select(e => e.FullName).FirstOrDefault(),
                db.Branchs.Where(b => b.Id == x.BranchId).Select(b => b.Name).FirstOrDefault())).ToListAsync();
    }

    private const string CsvColumns = "Tag,Name,Category,Brand,Model,SerialNumber,Supplier,PurchaseReference,PurchasedOn,WarrantyUntil,PurchaseCost,BookValue,Currency,BranchId,DepartmentId,EmployeeId,OfficeLocation,Condition,Status,Notes";

    public IActionResult OnGetSample()
    {
        return RegistrationCsv.Download("assets-sample", CsvColumns, [["IT-001", "Office laptop", "IT", null, null, null, null, null, null, null, 600, 600, "USD", 1, null, null, "IT office", "Good", AssetStatus.Available, null]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var records = await db.Assets.AsNoTracking().OrderBy(x => x.Tag).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("assets", CsvColumns, records.Select(x => new object?[] { x.Tag, x.Name, x.Category, x.Brand, x.Model, x.SerialNumber, x.Supplier, x.PurchaseReference, x.PurchasedOn, x.WarrantyUntil, x.PurchaseCost, x.BookValue, x.Currency, x.BranchId, x.DepartmentId, x.EmployeeId, x.OfficeLocation, x.Condition, x.Status, x.Notes }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload)
    {
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            Input = new Asset();
            Input.Tag = row.Get<string>("Tag");
            Input.Name = row.Get<string>("Name");
            Input.Category = row.Get<string>("Category");
            Input.Brand = row.Optional("Brand");
            Input.Model = row.Optional("Model");
            Input.SerialNumber = row.Optional("SerialNumber");
            Input.Supplier = row.Optional("Supplier");
            Input.PurchaseReference = row.Optional("PurchaseReference");
            Input.PurchasedOn = row.Get<DateTime?>("PurchasedOn");
            Input.WarrantyUntil = row.Get<DateTime?>("WarrantyUntil");
            Input.PurchaseCost = row.Get<decimal?>("PurchaseCost");
            Input.BookValue = row.Get<decimal?>("BookValue");
            Input.Currency = row.Get<string>("Currency");
            Input.BranchId = row.Get<int>("BranchId");
            Input.DepartmentId = row.Get<int?>("DepartmentId");
            Input.EmployeeId = row.Get<int?>("EmployeeId");
            Input.OfficeLocation = row.Optional("OfficeLocation");
            Input.Condition = row.Optional("Condition");
            Input.Status = row.Get<AssetStatus>("Status");
            Input.Notes = row.Optional("Notes");
            TryValidateModel(Input, "Input");
            if (!Enum.IsDefined(Input.Status))
                ModelState.AddModelError("Input.Status", "Choose a valid status.");
            return await OnPostAsync(null);
        }, () => RedirectToPage("./Edit", new { id = (int?)null }));
    }
}
