using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Operations;

public class MeetingReportModel(HrDbContext db) : PageModel
{
    public bool IsAdmin => db.IsAdmin;
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Scope { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    public List<ReportRow> Items { get; private set; } = [];
    public Dictionary<string, int> Totals { get; private set; } = [];
    public sealed class ReportRow
    {
        public int Id { get; init; }
        public bool CanOpen { get; init; }
        public string Title { get; init; } = "";
        public DateTime Starts { get; init; }
        public DateTime Ends { get; init; }
        public string Status { get; init; } = "";
        public int Invitees { get; init; }
    }
    private readonly DateTime now = DateTime.UtcNow;

    private bool ValidateFilters()
    {
        From ??= BusinessClock.Today.AddDays(-30);
        To ??= BusinessClock.Today.AddDays(30);
        if (From.Value.Year < 1900 || To.Value.Year >= 9999 || To.Value.Date < From.Value.Date ||
            To.Value.Date - From.Value.Date > TimeSpan.FromDays(365))
            ModelState.AddModelError("", "Choose a valid reporting period of up to 366 days.");
        Scope = Scope == "all" && IsAdmin ? "all" : Scope == "organized" ? "organized" : "mine";
        Status = Status is "Upcoming" or "In progress" or "Past" or "Cancelled" ? Status : null;
        return ModelState.IsValid;
    }

    private IQueryable<ReportRow> Report()
    {
        var actor = db.ActorId!;
        var start = OperationsPolicy.ToUtc(From!.Value.Date);
        var end = OperationsPolicy.ToUtc(To!.Value.Date.AddDays(1));
        var meetings = Scope == "all" && IsAdmin ? db.WorkMeetings : OperationsPolicy.Meetings(db, actor);
        if (Scope == "organized") meetings = meetings.Where(m => m.OwnerId == actor);
        return meetings.AsNoTracking().Where(m => m.StartsUtc >= start && m.StartsUtc < end)
            .Select(m => new ReportRow {
                Id = m.Id, CanOpen = m.OwnerId == actor || m.Participants.Any(p => p.UserId == actor),
                Title = m.Title, Starts = m.StartsUtc, Ends = m.EndsUtc,
                Status = m.Cancelled ? "Cancelled" : m.EndedUtc != null ? "Past" : m.StartsUtc > now ? "Upcoming" : m.EndsUtc > now ? "In progress" : "Past",
                Invitees = m.Participants.Count
            });
    }

    private IQueryable<ReportRow> Filter(IQueryable<ReportRow> report)
    {
        var status = Status;
        return status == null ? report : report.Where(row => row.Status == status);
    }

    public async Task OnGetAsync()
    {
        Response.Headers.CacheControl = "no-store";
        if (!ValidateFilters()) return;
        var report = Report();
        Totals = await report.GroupBy(r => r.Status).Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
        Items = await Filter(report).OrderByDescending(r => r.Starts).Take(150).ToListAsync();
    }

    public async Task<IActionResult> OnGetCsvAsync(string format = "csv")
    {
        Response.Headers.CacheControl = "no-store";
        if (!ValidateFilters()) return BadRequest("Invalid date range.");
        var items = await Filter(Report()).OrderByDescending(r => r.Starts).Take(10001).ToListAsync();
        if (items.Count > 10000) return BadRequest("Choose a narrower reporting period.");
        var rows = new List<string[]> { new[] { "Meeting", "Start (EAT)", "End (EAT)", "Status", "Invited employees" } };
        rows.AddRange(items.Select(item => new[] { item.Title, OperationsPolicy.Local(item.Starts).ToString("yyyy-MM-dd HH:mm"),
            OperationsPolicy.Local(item.Ends).ToString("yyyy-MM-dd HH:mm"), item.Status, item.Invitees.ToString() }));
        return ExportService.Report(rows, "meeting-report", format);
    }
}
