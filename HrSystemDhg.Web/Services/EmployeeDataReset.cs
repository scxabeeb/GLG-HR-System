using System.Security.Cryptography;
using System.Text.Json;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;

// This maintenance operation is deliberately restricted to the administrator page.
public sealed class EmployeeDataReset(HrDbContext db, IWebHostEnvironment env, DocumentService documents)
{
    public record Preview(int Employees, int Attendance, int Salaries, int PayrollLines, int LeaveRequests, int EmployeeAccounts, int PreservedAdmins);
    public record Backup(Guid Id, DateTime CreatedUtc, string ActorId, string Fingerprint, Preview Counts, Dictionary<string, JsonElement> Tables);
    string Root => Path.Combine(env.ContentRootPath, "App_Data", "hr-reset-backups");
    static readonly JsonSerializerOptions Json = new() { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles };
    async Task<string[]> AccountIdsAsync()
    {
        var admins = db.UserRoles.Where(r => db.Roles.Any(role => role.Id == r.RoleId && (role.Name == "Admin" || role.Name == "General Secretary"))).Select(r => r.UserId);
        return await db.Users.Where(u => !admins.Contains(u.Id)).Select(u => u.Id).ToArrayAsync();
    }
    public async Task<Preview> PreviewAsync() => new(await db.Employees.IgnoreQueryFilters().CountAsync(), await db.AttendanceDays.IgnoreQueryFilters().CountAsync(),
        await db.SalaryVersions.IgnoreQueryFilters().CountAsync(), await db.PayrollLines.IgnoreQueryFilters().CountAsync(), await db.LeaveRequests.IgnoreQueryFilters().CountAsync(),
        (await AccountIdsAsync()).Length, await db.UserRoles.Where(r => db.Roles.Any(role => role.Id == r.RoleId && (role.Name == "Admin" || role.Name == "General Secretary"))).Select(r=>r.UserId).Distinct().CountAsync());
    async Task<Dictionary<string, JsonElement>> SnapshotAsync()
    {
        var tables = new Dictionary<string, JsonElement>();
        async Task Add<T>(string name) where T : class
        {
            var data = await db.Set<T>().IgnoreQueryFilters().AsNoTracking().ToListAsync();
            var rows = data.Select(row => JsonSerializer.SerializeToElement(row, Json)).OrderBy(row => row.GetRawText()).ToArray();
            tables[name] = JsonSerializer.SerializeToElement(rows, Json);
        }
        await Add<Microsoft.AspNetCore.Identity.IdentityRole>("Roles");
        await Add<Employee>("Employees"); await Add<EmployeeAssignment>("EmployeeAssignments"); await Add<Education>("Educations");
        await Add<WorkExperience>("WorkExperiences"); await Add<EmployeeDocument>("EmployeeDocuments"); await Add<SalaryVersion>("SalaryVersions");
        await Add<AttendanceDay>("AttendanceDays"); await Add<ZkPunch>("ZkPunchs"); await Add<ZkPunchReview>("ZkPunchReviews"); await Add<ZkDeviceCommand>("ZkDeviceCommands");
        await Add<LeaveRequest>("LeaveRequests"); await Add<LeaveBalance>("LeaveBalances"); await Add<CorrectionRequest>("CorrectionRequests"); await Add<OvertimeApproval>("OvertimeApprovals");
        await Add<PayrollPeriod>("PayrollPeriods"); await Add<PayrollLine>("PayrollLines"); await Add<PayrollDeduction>("PayrollDeductions"); await Add<Payslip>("Payslips");
        await Add<EmployeeNumberSequence>("EmployeeNumberSequences"); await Add<ApplicationUser>("Users");
        await Add<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>("UserRoles"); await Add<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>("UserClaims");
        await Add<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>("UserLogins"); await Add<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>("UserTokens");
        await Add<UserBranchScope>("UserBranchScopes"); await Add<UserModuleRestriction>("UserModuleRestrictions"); await Add<WhatsAppPreference>("WhatsAppPreferences");
        await Add<AgendaItem>("AgendaItems"); await Add<MeetingParticipant>("MeetingParticipants"); await Add<WorkTaskTeamMember>("TeamMembers"); await Add<OperationsNotification>("Notifications");
        tables["AssetEmployees"] = JsonSerializer.SerializeToElement(await db.Assets.IgnoreQueryFilters().OrderBy(x=>x.Id).Select(x=>new{x.Id,x.EmployeeId}).ToListAsync());
        tables["AssetActivityEmployees"] = JsonSerializer.SerializeToElement(await db.AssetActivities.IgnoreQueryFilters().OrderBy(x=>x.Id).Select(x=>new{x.Id,x.EmployeeId}).ToListAsync());
        tables["VehicleDrivers"] = JsonSerializer.SerializeToElement(await db.FleetVehicles.OrderBy(x=>x.Id).Select(x=>new{x.Id,x.DriverId}).ToListAsync());
        tables["TripDrivers"] = JsonSerializer.SerializeToElement(await db.FleetTrips.OrderBy(x=>x.Id).Select(x=>new{x.Id,x.DriverId}).ToListAsync());
        return tables;
    }
    static string Fingerprint(Dictionary<string, JsonElement> tables) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(tables, Json)));
    string BackupPath(Guid id) => Path.Combine(Root, id.ToString("N") + ".json");
    public async Task<Backup> CreateBackupAsync(string actor)
    {
        var tables = await SnapshotAsync();
        var backup = new Backup(Guid.NewGuid(), DateTime.UtcNow, actor, Fingerprint(tables), await PreviewAsync(), tables);
        Directory.CreateDirectory(Root);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var path = BackupPath(backup.Id);
        await File.WriteAllBytesAsync(path, JsonSerializer.SerializeToUtf8Bytes(backup, Json));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        // Preserve actual uploaded CVs, certificates and photos alongside the data backup.
        var files = Path.Combine(Root, backup.Id.ToString("N") + "-documents"); Directory.CreateDirectory(files);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(files, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        foreach (var document in await db.EmployeeDocuments.IgnoreQueryFilters().AsNoTracking().ToListAsync())
        {
            var name = Path.GetFileName(document.StoragePath); if (string.IsNullOrEmpty(name)) continue;
            var source = Path.Combine(documents.Root, name);
            if (File.Exists(source)) File.Copy(source, Path.Combine(files, name), true);
        }
        return backup;
    }
    public async Task<Backup?> ReadBackupAsync(Guid id) => File.Exists(BackupPath(id)) ? JsonSerializer.Deserialize<Backup>(await File.ReadAllBytesAsync(BackupPath(id)), Json) : null;
    public async Task ResetAsync(Guid backupId, string actor)
    {
        var backup = await ReadBackupAsync(backupId) ?? throw new InvalidOperationException("Create a backup before resetting data.");
        if (backup.ActorId != actor || DateTime.UtcNow - backup.CreatedUtc > TimeSpan.FromMinutes(15)) throw new InvalidOperationException("Create a fresh backup using your administrator account.");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (backup.Fingerprint != Fingerprint(await SnapshotAsync())) throw new InvalidOperationException("Records changed since the backup. Create a new backup before resetting.");
        var adminIds = await db.UserRoles.Where(r => db.Roles.Any(role => role.Id == r.RoleId && role.Name == "Admin")).Select(r => r.UserId).ToArrayAsync();
        if (!adminIds.Contains(actor) || !await db.Users.AnyAsync(u => u.Id == actor && u.IsEnabled)) throw new InvalidOperationException("An active administrator account must be retained.");
        var users = await AccountIdsAsync();
        var pins = await db.Employees.IgnoreQueryFilters().Where(x=>x.ZkPin!=null).Select(x=>x.ZkPin!).ToListAsync();
        pins.AddRange(await db.ZkPunchs.Select(x=>x.Pin).ToListAsync()); pins.AddRange(await db.ZkPunchReviews.Select(x=>x.Pin).ToListAsync());
        pins.AddRange(await db.ZkDeviceCommands.Select(x=>x.Pin).ToListAsync());
        var nextPin = Math.Max(await db.EmployeeNumberSequences.MaxAsync(x=>x.NextPin), pins.Select(x=>int.TryParse(x,out var pin)&&pin>=0&&pin<=999999999?pin+1:1).DefaultIfEmpty(1).Max());
        var files = await db.EmployeeDocuments.IgnoreQueryFilters().Select(x => x.StoragePath).ToListAsync();
        await db.PayrollDeductions.ExecuteDeleteAsync(); await db.Payslips.ExecuteDeleteAsync(); await db.PayrollLines.IgnoreQueryFilters().ExecuteDeleteAsync(); await db.PayrollPeriods.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.CorrectionRequests.IgnoreQueryFilters().ExecuteDeleteAsync(); await db.OvertimeApprovals.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.LeaveRequests.IgnoreQueryFilters().ExecuteDeleteAsync(); await db.LeaveBalances.IgnoreQueryFilters().ExecuteDeleteAsync(); await db.AttendanceDays.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.SalaryVersions.IgnoreQueryFilters().ExecuteDeleteAsync(); await db.Educations.IgnoreQueryFilters().ExecuteDeleteAsync(); await db.WorkExperiences.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.EmployeeDocuments.IgnoreQueryFilters().ExecuteDeleteAsync(); await db.EmployeeAssignments.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.ZkDeviceCommands.ExecuteDeleteAsync(); await db.ZkPunchReviews.ExecuteDeleteAsync(); await db.ZkPunchs.ExecuteDeleteAsync();
        await db.Assets.IgnoreQueryFilters().Where(x=>x.EmployeeId!=null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.EmployeeId,(int?)null));
        await db.AssetActivities.IgnoreQueryFilters().Where(x=>x.EmployeeId!=null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.EmployeeId,(int?)null));
        await db.FleetVehicles.Where(x=>x.DriverId!=null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.DriverId,(int?)null));
        await db.FleetTrips.Where(x=>x.DriverId!=null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.DriverId,(int?)null));
        await db.OperationsNotifications.Where(n=>users.Contains(n.RecipientId) || n.Kind==OperationsNoticeKind.LeaveDecided || n.Kind==OperationsNoticeKind.PayslipPublished || n.Kind==OperationsNoticeKind.AttendanceCorrectionDecided).ExecuteDeleteAsync();
        await db.WhatsAppPreferences.Where(x=>users.Contains(x.UserId)).ExecuteDeleteAsync();
        await db.WorkTaskTeamMembers.Where(x=>users.Contains(x.UserId)).ExecuteDeleteAsync(); await db.Set<MeetingParticipant>().Where(x=>users.Contains(x.UserId)).ExecuteDeleteAsync();
        await db.AgendaItems.Where(x=>users.Contains(x.OwnerId)).ExecuteDeleteAsync();
        await db.UserBranchScopes.Where(x=>users.Contains(x.UserId)).ExecuteDeleteAsync(); await db.UserModuleRestrictions.Where(x=>users.Contains(x.UserId)).ExecuteDeleteAsync();
        await db.Users.Where(x=>x.EmployeeId!=null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.EmployeeId,(int?)null));
        await db.Users.Where(x=>users.Contains(x.Id)).ExecuteDeleteAsync(); await db.Employees.IgnoreQueryFilters().ExecuteDeleteAsync();
        // Employee numbers restart; terminal PINs deliberately remain monotonic to avoid old fingerprints mapping to new people.
        await db.EmployeeNumberSequences.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.NextNumber,1).SetProperty(x=>x.NextPin,nextPin));
        db.AuditLogs.Add(new(){Actor=actor,Action=$"Employee data reset: backup {backupId:N}; {backup.Counts.Employees} employees; {users.Length} employee accounts; Admin and General Secretary accounts and configuration preserved"});
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        foreach (var name in files.Select(Path.GetFileName).OfType<string>())
            if (!string.IsNullOrEmpty(name)) { try { File.Delete(Path.Combine(documents.Root,name)); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { /* The private backup remains available. */ } }
    }
}
