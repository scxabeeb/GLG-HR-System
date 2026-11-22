using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;
public class LeaveService(HrDbContext db, CalendarService calendar, DocumentService? documents = null)
{
    public const string Submitted = "Submitted by HR";

    public async Task<(decimal Used, decimal Remaining)> BalanceAsync(int employeeId, int typeId, int year, DateTime? asOf = null)
    {
        var type = await db.LeaveTypes.SingleAsync(x => x.Id == typeId);
        var credit = await db.LeaveBalances.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveTypeId == typeId && x.Year == year);
        decimal allowance = credit?.Days ?? type.AnnualDays;
        if (type.AccrueMonthly && credit == null)
            allowance = type.AnnualDays * ((asOf ?? BusinessClock.Today).Year > year ? 12 : (asOf ?? BusinessClock.Today).Year < year ? 0 : (asOf ?? BusinessClock.Today).Month) / 12;
        decimal used = 0;
        foreach (var r in await db.LeaveRequests.Where(x => x.EmployeeId == employeeId && x.LeaveTypeId == typeId && x.Status == "Approved" && x.From.Year <= year && x.To.Year >= year).ToListAsync())
            used += await calendar.LeaveDaysAsync(r, year);
        return (decimal.Round(used, 2), decimal.Round(allowance - used, 2));
    }

    public async Task<decimal> RemainingAsync(int employeeId, int typeId, int year, DateTime? asOf = null) =>
        (await BalanceAsync(employeeId, typeId, year, asOf)).Remaining;

    public async Task ForwardAsync(int id, string actor)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var request = await db.LeaveRequests.SingleOrDefaultAsync(x => x.Id == id);
        if (request == null || request.Status is not ("Pending" or "Manager approved"))
            throw new InvalidOperationException("Select a leave request waiting for HR submission.");
        await ValidateAsync(request);
        request.Status = Submitted;
        db.AuditLogs.Add(new() { Actor = actor, Action = $"HR submitted leave {id} for final approval" });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task ValidateAsync(LeaveRequest r, bool approval = false)
    {
        if (r.From == default || r.To < r.From || (r.To - r.From).TotalDays > 365 || string.IsNullOrWhiteSpace(r.Reason))
            throw new InvalidOperationException("Enter a reason and valid date range (up to one year).");
        if (r.StartTime.HasValue != r.EndTime.HasValue || (r.StartTime.HasValue && (r.From.Date != r.To.Date || r.EndTime == r.StartTime)))
            throw new InvalidOperationException("Partial-day leave requires a single date and valid start/end times.");
        var type = await db.LeaveTypes.SingleAsync(x => x.Id == r.LeaveTypeId);
        var e = await db.Employees.SingleAsync(x => x.Id == r.EmployeeId);
        if (!string.IsNullOrWhiteSpace(type.GenderRestriction) && type.GenderRestriction != e.Gender || !string.IsNullOrWhiteSpace(type.EmploymentRestriction) && type.EmploymentRestriction != e.EmploymentType)
            throw new InvalidOperationException("This leave type is not available for this employee.");
        if (type.RequiresDocument && (r.DocumentId == null || !await db.EmployeeDocuments.AnyAsync(x => x.Id == r.DocumentId && x.EmployeeId == r.EmployeeId)))
            throw new InvalidOperationException("Attach a supporting document before submitting this leave type.");
        var overlaps = await db.LeaveRequests.Where(x => x.Id != r.Id && x.EmployeeId == r.EmployeeId && x.Status != "Rejected" && x.Status != "Cancelled" && x.From <= r.To && x.To >= r.From).ToListAsync();
        if (overlaps.Any(x => !x.StartTime.HasValue || !r.StartTime.HasValue || (x.EndTime < x.StartTime || r.EndTime < r.StartTime || x.StartTime < r.EndTime && x.EndTime > r.StartTime)))
            throw new InvalidOperationException("This request overlaps existing leave.");
        if (r.StartTime != null)
        {
            var rule = await calendar.RuleAsync(r.EmployeeId, r.From);
            var shift = CalendarService.Window(rule, r.From);
            var segment = CalendarService.LeaveWindow(r, rule, r.From);
            if (segment.Start < shift.Start || segment.End > shift.End)
                throw new InvalidOperationException("Partial leave times must be within the scheduled shift.");
        }

        var amount = await calendar.LeaveDaysAsync(r);
        if (amount <= 0 || (!r.StartTime.HasValue && amount < type.MinDays) || amount > type.MaxDays)
            throw new InvalidOperationException("The working-day duration is outside this leave type's limits.");
        if (type.EnforceBalance)
            for (var year = r.From.Year; year <= r.To.Year; year++)
            {
                var required = await calendar.LeaveDaysAsync(r, year);
                if (required > await RemainingAsync(r.EmployeeId, r.LeaveTypeId, year, r.From))
                    throw new InvalidOperationException("Insufficient leave balance for " + year);
            }
    }

    public async Task SubmitAsync(LeaveRequest r, string actor, IFormFile? upload = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        string? uploadedPath = null;
        try
        {
            if (upload != null && documents != null)
            {
                r.DocumentId = await documents.StoreAsync(r.EmployeeId, upload, "Leave supporting document", null, actor);
                var document = await db.EmployeeDocuments.FindAsync(r.DocumentId.Value);
                if (document != null) uploadedPath = Path.Combine(documents.Root, document.StoragePath);
            }
            await ValidateAsync(r);
            r.Status = "Pending";
            db.LeaveRequests.Add(r);
            db.AuditLogs.Add(new() { Actor = actor, Action = $"Recorded leave request for employee {r.EmployeeId}" });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            if (uploadedPath != null) File.Delete(uploadedPath);
            throw;
        }
    }

    public async Task DecideAsync(int id, string decision, string reason, string actor, int? approverEmployeeId, bool management)
    {
        if (decision is not ("Approved" or "Rejected"))
            throw new InvalidOperationException("Invalid decision.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var r = await db.LeaveRequests.SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new InvalidOperationException("Leave request is unavailable.");
        if (!management || r.Status != Submitted)
            throw new InvalidOperationException("HR must submit the request before final approval.");
        if (decision == "Approved")
            await ValidateAsync(r, true);
        else if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A rejection reason is required.");
        r.Status = decision;

        r.DecisionReason = reason;
        r.DecidedBy = actor;
        r.DecidedAt = DateTime.UtcNow;
        await OperationsNotices.AddEmployeeUpdatesAsync(db, [r.EmployeeId], OperationsNoticeKind.LeaveDecided,
            $"{r.Status} · {r.From:dd MMM yyyy} to {r.To:dd MMM yyyy}");
        db.AuditLogs.Add(new() { Actor = actor, Action = $"Leave {id}: {r.Status}; {reason}" });
        await db.SaveChangesAsync();
        if (r.Status == "Approved")
        {
            var attendance = new AttendanceService(db, calendar);
            for (var date = r.From.Date; date <= r.To.Date && date <= BusinessClock.Today; date = date.AddDays(1))
                await attendance.RebuildAsync(r.EmployeeId, date);
        }
        await tx.CommitAsync();
    }
}
