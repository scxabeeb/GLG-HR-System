using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;
public class DocumentService(HrDbContext db, IWebHostEnvironment env)
{
    public string Root => Path.Combine(env.ContentRootPath, "App_Data", "documents");

    public async Task<int> StoreAsync(int employeeId, IFormFile file, string type, DateTime? expiry, string actor)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == employeeId))
            throw new InvalidOperationException("Employee not found.");
        if (file.Length <= 0 || file.Length > 10 * 1024 * 1024)
            throw new InvalidOperationException("Files must be between 1 byte and 10 MB.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var content = ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => throw new InvalidOperationException("Only PDF, JPG and PNG files are allowed.")};
        using var input = file.OpenReadStream();
        var signature = new byte[8];
        var count = await input.ReadAsync(signature);
        bool valid = ext == ".pdf" ? count >= 5 && System.Text.Encoding.ASCII.GetString(signature, 0, 5) == "%PDF-" : ext == ".png" ? count == 8 && signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) : count >= 3 && signature[0] == 255 && signature[1] == 216 && signature[2] == 255;
        if (!valid)
            throw new InvalidOperationException("The file content does not match its type.");
        Directory.CreateDirectory(Root);
        var name = Guid.NewGuid().ToString("N") + ext;
        var path = Path.Combine(Root, name);
        await using (var output = File.Create(path))
        {
            await output.WriteAsync(signature.AsMemory(0, count));
            await input.CopyToAsync(output);
        }

        var doc = new EmployeeDocument
        {
            EmployeeId = employeeId,
            Type = type,
            StoragePath = name,
            OriginalName = Path.GetFileName(file.FileName),
            ContentType = content,
            ExpiryDate = expiry
        };
        db.EmployeeDocuments.Add(doc);
        db.AuditLogs.Add(new() { Actor = actor, Action = $"Uploaded {type} document for employee {employeeId}" });
        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            File.Delete(path);
            throw;
        }

        return doc.Id;
    }
}
