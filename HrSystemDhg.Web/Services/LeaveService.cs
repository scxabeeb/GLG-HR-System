using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;
public class LeaveService(HrDbContext db, CalendarService calendar)
{
    public async Task<decimal> RemainingAsync(int employeeId, int typeId, int year, DateTime? asOf = null)
    {
        var type = await db.LeaveTypes.SingleAsync(x => x.Id == typeId);
        var credit = await db.LeaveBalances.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveTypeId == typeId && x.Year == year);
        decimal allowance = credit?.Days ?? type.AnnualDays;
        if (type.AccrueMonthly && credit == null)
            allowance = type.AnnualDays * Math.Min(12, (asOf ?? BusinessClock.Today).Year > year ? 12 : (asOf ?? BusinessClock.Today).Month) / 12;
        decimal used = 0;
        foreach (var r in await db.LeaveRequests.Where(x => x.EmployeeId == employeeId && x.LeaveTypeId == typeId && x.Status == "Approved" && x.From.Year <= year && x.To.Year >= year).ToListAsync())
            used += await calendar.LeaveDaysAsync(r, year);
        return decimal.Round(allowance - used, 2);
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

    public async Task SubmitAsync(LeaveRequest r, string actor)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await ValidateAsync(r);
        r.Status = "Pending";
        db.LeaveRequests.Add(r);
        db.AuditLogs.Add(new() { Actor = actor, Action = $"Submitted leave for employee {r.EmployeeId}" });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task DecideAsync(int id, string decision, string reason, string actor, int? approverEmployeeId, bool management)
    {
        if (decision is not ("Approved" or "Rejected"))
            throw new InvalidOperationException("Invalid decision.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var r = await db.LeaveRequests.SingleAsync(x => x.Id == id);
        if (r.Status is not ("Pending" or "Manager approved"))
            throw new InvalidOperationException("Request already decided.");
        var setting = await db.Settings.FirstOrDefaultAsync() ?? new();
        var e = await db.Employees.SingleAsync(x => x.Id == r.EmployeeId);
        if (decision == "Approved")
        {
            await ValidateAsync(r, true);
            if (setting.ManagerApproval && !management && r.Status == "Pending")
            {
                if (e.ManagerId != approverEmployeeId)
                    throw new InvalidOperationException("The reporting manager must approve first.");
                r.Status = "Manager approved";
            }
            else
                r.Status = "Approved";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("A rejection reason is required.");
            r.Status = "Rejected";
        }

        r.DecisionReason = reason;
        r.DecidedBy = actor;
        r.DecidedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new() { Actor = actor, Action = $"Leave {id}: {r.Status}; {reason}" });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
