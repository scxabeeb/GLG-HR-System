using System.Data;
using HrSystemDhg.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;

// Finalizes existing summaries only; never creates punches or recalculates payroll.
public sealed class AttendanceDeadlineWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<AttendanceDeadlineWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Attendance:AutomaticClosingEnabled", true)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ClosePendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Attendance closing will retry on its next cycle."); }
            try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task ClosePendingAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var attendance = scope.ServiceProvider.GetRequiredService<AttendanceService>();
        var calendar = scope.ServiceProvider.GetRequiredService<CalendarService>();
        var today = attendance.LocalNow.Date;
        var since = today.AddDays(-31);
        var actualMinutesFrom = AttendanceService.ActualMinutesEffectiveFrom;
        var candidates = await db.AttendanceDays.AsNoTracking()
            .Where(d => d.Date <= today && ((d.Date >= actualMinutesFrom && d.CalculationVersion < AttendanceService.CalculationVersion) || d.Status.StartsWith("Pending") || d.Status == "Not due" ||
                (d.Date >= since && (d.Status == "Incomplete punch" ||
                 ((d.Status == "Absent" || d.Status == "On leave (partial)") && (d.FirstIn == null || d.LastOut == null) && (d.FirstIn != null || d.LastOut != null))))))
            .OrderBy(d => d.CalculationVersion >= AttendanceService.CalculationVersion)
            .ThenByDescending(d => d.Status.StartsWith("Pending") || d.Status == "Not due")
            .ThenByDescending(d => d.Date).Select(d => new { d.Id, d.EmployeeId, d.Date, d.CalculationVersion }).Take(500).ToListAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            db.ChangeTracker.Clear();
            try
            {
                var upgrade = candidate.Date >= actualMinutesFrom && candidate.CalculationVersion < AttendanceService.CalculationVersion;
                if (!upgrade)
                {
                    var rule = await calendar.RuleAsync(candidate.EmployeeId,candidate.Date);
                    if (!rule.UsePunchWindows) continue;
                }
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                // Serialize with device/correction updates, then rebuild from the latest raw punches.
                var locked = await db.AttendanceDays.FromSqlInterpolated($"SELECT * FROM AttendanceDays WHERE Id = {candidate.Id} FOR UPDATE").IgnoreQueryFilters().ToListAsync(cancellationToken);
                if (locked.Count == 0) continue;
                await attendance.RebuildAsync(candidate.EmployeeId, candidate.Date);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (InvalidOperationException ex) { logger.LogWarning(ex, "Unable to close attendance summary {SummaryId}; check its effective assignment and rule.", candidate.Id); }
        }
    }
}
