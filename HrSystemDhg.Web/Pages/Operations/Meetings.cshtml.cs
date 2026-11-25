using System.ComponentModel.DataAnnotations;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Operations;
public class MeetingsModel(HrDbContext db, RoomLinks links, OperationsNotices notices) : PageModel
{
    public string Actor => db.ActorId!;
    public bool CanOrganize => db.IsStaff;
    [BindProperty(SupportsGet = true)] public string? Mode { get; set; }
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    public bool Managing => CanOrganize && Mode == "manage";
    public List<WorkMeeting> Items { get; private set; } = [];
    public record Invitee(string Id,string Name,string Email,string EmployeeNumber);
    public List<Invitee> Invitees { get; private set; } = [];
    [BindProperty] public MeetingInput Input { get; set; } = new();
    public class MeetingInput
    {
        public int Id { get; set; }
        [Required, StringLength(160)] public string Title { get; set; } = "";
        [StringLength(2500)] public string? Agenda { get; set; }
        public bool InPerson { get; set; }
        [StringLength(250)] public string? Location { get; set; }
        [StringLength(2048)] public string? JoinUrl { get; set; }
        [Required] public DateTime? Starts { get; set; }
        [Required] public DateTime? Ends { get; set; }
        public string[] Guests { get; set; } = [];
    }

    public string? GuestLink(WorkMeeting meeting) =>
        Url.Page("./MeetingRoom", null, new { id = meeting.Id, guest = links.Issue(meeting) }, Request.Scheme);

    private async Task Load()
    {
        Response.Headers.CacheControl = "no-store";
        var now = DateTime.UtcNow;
        var query = OperationsPolicy.Meetings(db, Actor);
        if (Managing) query = query.Where(m => m.OwnerId == Actor);
        Period = Period is "upcoming" or "past" ? Period : null;
        if (Period == "upcoming") query = query.Where(m => !m.Cancelled && m.EndedUtc == null && m.EndsUtc > now);
        if (Period == "past") query = query.Where(m => m.Cancelled || m.EndedUtc != null || m.EndsUtc <= now);
        Items = await query.AsNoTracking().Include(m => m.Participants)
            .OrderBy(m => m.Cancelled || m.EndedUtc != null || m.EndsUtc < now).ThenBy(m => m.EndsUtc < now ? DateTime.MaxValue : m.StartsUtc)
            .ThenByDescending(m => m.StartsUtc).Take(150).ToListAsync();
        if (Managing) Invitees = (await OperationsPolicy.EligiblePeople(db, "meetings").Where(u => u.Id != Actor)
            .Select(u => new Invitee(u.Id,
                db.Employees.Where(e=>e.Id==u.EmployeeId).Select(e=>e.FullName).FirstOrDefault() ?? u.UserName ?? u.Email ?? "Employee",
                u.Email ?? "",db.Employees.Where(e=>e.Id==u.EmployeeId).Select(e=>e.EmployeeNumber).FirstOrDefault() ?? ""))
            .ToListAsync()).OrderBy(u=>u.Name).ToList();
    }

    public async Task<IActionResult> OnGetAsync(int? edit)
    {
        if (edit.HasValue)
        {
            if (!CanOrganize) return Forbid();
            Mode = "manage";
            var meeting = await db.WorkMeetings.AsNoTracking().Include(m => m.Participants)
                .SingleOrDefaultAsync(m => m.Id == edit && m.OwnerId == Actor && !m.Cancelled && m.EndedUtc == null);
            if (meeting == null) return NotFound();
            Input = new() { Id = meeting.Id, Title = meeting.Title, Agenda = meeting.Agenda, JoinUrl = meeting.JoinUrl, InPerson = meeting.Location != null, Location = meeting.Location,
                Starts = OperationsPolicy.Local(meeting.StartsUtc), Ends = OperationsPolicy.Local(meeting.EndsUtc),
                Guests = meeting.Participants.Select(p => p.UserId).ToArray() };
        }
        await Load();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!CanOrganize) return Forbid();
        Mode = "manage";
        if (Input.InPerson && string.IsNullOrWhiteSpace(Input.Location))
            ModelState.AddModelError("Input.Location", "Enter a location for the in-person meeting.");
        if (!Input.InPerson && !string.IsNullOrWhiteSpace(Input.JoinUrl) && !OperationsPolicy.IsJoinUrl(Input.JoinUrl.Trim()))
            ModelState.AddModelError("Input.JoinUrl", "Paste a Microsoft Teams or Google Meet join link.");
        if (Input.Starts is not { Year: >= 1900 and < 9999 } || Input.Ends is not { Year: >= 1900 and < 9999 } || Input.Ends <= Input.Starts)
            ModelState.AddModelError("Input.Ends", "Select a valid start time and an end time after it.");
        var guests = Input.Guests.Where(id => id != Actor).Distinct().ToArray();
        if (guests.Length > 100 || guests.Any(id => id.Length > 255) ||
            await OperationsPolicy.EligiblePeople(db).CountAsync(u => guests.Contains(u.Id)) != guests.Length)
            ModelState.AddModelError("Input.Guests", "Choose up to 100 active participants in your scope.");
        if (!ModelState.IsValid) { await Load(); return Page(); }

        var meeting = Input.Id == 0 ? new WorkMeeting { OwnerId = Actor } :
            await db.WorkMeetings.Include(m => m.Participants).SingleOrDefaultAsync(m => m.Id == Input.Id && m.OwnerId == Actor && !m.Cancelled && m.EndedUtc == null);
        if (meeting == null) return NotFound();
        var previous = meeting.Participants.Select(p => p.UserId).ToArray();
        var location = Input.InPerson ? Input.Location!.Trim() : null;
        var joinUrl = Input.InPerson ? "" : Input.JoinUrl?.Trim() ?? "";
        var changed = meeting.Title != Input.Title.Trim() || meeting.Agenda != (Input.Agenda?.Trim() ?? "") ||
            meeting.Location != location || meeting.JoinUrl != joinUrl ||
            meeting.StartsUtc != OperationsPolicy.ToUtc(Input.Starts!.Value) || meeting.EndsUtc != OperationsPolicy.ToUtc(Input.Ends!.Value);
        var additions = guests.Except(previous).ToArray();
        if (await OperationsPolicy.EligiblePeople(db, "meetings").CountAsync(u => additions.Contains(u.Id)) != additions.Length)
        {
            ModelState.AddModelError("Input.Guests", "New invitees must have access to the meeting module.");
            await Load();
            return Page();
        }
        meeting.Title = Input.Title.Trim();
        meeting.Agenda = Input.Agenda?.Trim() ?? "";
        meeting.Location = location;
        meeting.JoinUrl = joinUrl;
        meeting.StartsUtc = OperationsPolicy.ToUtc(Input.Starts!.Value);
        meeting.EndsUtc = OperationsPolicy.ToUtc(Input.Ends!.Value);
        meeting.Participants.RemoveAll(p => !guests.Contains(p.UserId));
        foreach (var guest in guests.Except(meeting.Participants.Select(p => p.UserId)))
            meeting.Participants.Add(new() { UserId = guest });
        if (Input.Id == 0) db.WorkMeetings.Add(meeting);
        await notices.AddAsync(guests.Except(previous), OperationsNoticeKind.MeetingInvitation, meeting: meeting);
        if (Input.Id != 0 && changed)
            await notices.AddAsync(guests.Intersect(previous), OperationsNoticeKind.MeetingUpdated, meeting: meeting);
        db.AuditLogs.Add(new() { Actor = Actor, Action = Input.Id == 0 ? "Scheduled meeting" : $"Updated meeting {meeting.Id}" });
        await db.SaveChangesAsync();
        TempData["OperationsNotice"] = "Meeting saved. Invited employees can find it here.";
        return RedirectToPage(new { mode = "manage" });
    }

    public async Task<IActionResult> OnPostEndAsync(int id, [FromServices] MeetingRooms rooms)
    {
        if (!CanOrganize) return Forbid();
        var owned = db.WorkMeetings.Where(m => m.Id == id && m.OwnerId == Actor);
        if (!await owned.AnyAsync()) return NotFound();

        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var changed = await owned.Where(m => !m.Cancelled && m.EndedUtc == null &&
                m.StartsUtc <= now && m.EndsUtc > now)
            .ExecuteUpdateAsync(update => update.SetProperty(m => m.EndedUtc, now));
        if (changed == 0)
        {
            TempData["OperationsNotice"] = "This meeting is no longer in progress.";
            return RedirectToPage(new { Mode, Period });
        }

        db.AuditLogs.Add(new() { Actor = Actor, Action = $"Ended meeting {id} from the meeting list" });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        await rooms.BroadcastEndedAsync(id);
        TempData["OperationsNotice"] = "Meeting ended in Garowe City Management System.";
        return RedirectToPage(new { Mode, Period });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        if (!CanOrganize) return Forbid();
        var meeting = await db.WorkMeetings.SingleOrDefaultAsync(m => m.Id == id && m.OwnerId == Actor);
        if (meeting == null) return NotFound();
        if (meeting.Cancelled || meeting.EndedUtc != null) return RedirectToPage(new { mode = "manage" });
        meeting.Cancelled = true;
        var participants = await db.Set<MeetingParticipant>().Where(p => p.MeetingId == id).Select(p => p.UserId).ToListAsync();
        await notices.AddAsync(participants, OperationsNoticeKind.MeetingCancelled, meeting: meeting);
        db.AuditLogs.Add(new() { Actor = Actor, Action = $"Cancelled meeting {id} in HR" });
        await db.SaveChangesAsync();
        TempData["OperationsNotice"] = meeting.Location != null ? "In-person meeting cancelled." : meeting.JoinUrl.Length == 0 ? "Meeting cancelled. Its room and guest link are closed." : "Cancelled in HR. Cancel the provider invitation separately.";
        return RedirectToPage(new { mode = "manage" });
    }

    private const string CsvColumns = "Title,Agenda,InPerson,Location,JoinUrl,Starts,Ends,Guests";

    public IActionResult OnGetSample()
    {
        if (!CanOrganize) return Forbid();
        return RegistrationCsv.Download("meetings-sample", CsvColumns, [["Team meeting", "Review plans", true, "Meeting room", null, BusinessClock.Today.AddDays(1).AddHours(9), BusinessClock.Today.AddDays(1).AddHours(10), null]]);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        if (!CanOrganize) return Forbid();
        var records = await db.WorkMeetings.AsNoTracking().Where(x => x.OwnerId == Actor).Include(x => x.Participants).OrderBy(x => x.StartsUtc).ToListAsync();
        Response.Headers.CacheControl = "no-store";
        return RegistrationCsv.Download("meetings", CsvColumns, records.Select(x => new object?[] { x.Title, x.Agenda, x.Location != null, x.Location, x.JoinUrl, OperationsPolicy.Local(x.StartsUtc), OperationsPolicy.Local(x.EndsUtc), string.Join(";", x.Participants.Select(p => p.UserId)) }));
    }

    public Task<IActionResult> OnPostImportAsync(IFormFile? upload)
    {
        if (!CanOrganize) return Task.FromResult<IActionResult>(Forbid());
        return RegistrationCsv.ImportAsync(this, db, upload, CsvColumns, async row =>
        {
            Input = new MeetingInput();
            Input.Title = row.Get<string>("Title");
            Input.Agenda = row.Optional("Agenda");
            Input.InPerson = row.Get<bool>("InPerson");
            Input.Location = row.Optional("Location");
            Input.JoinUrl = row.Optional("JoinUrl");
            Input.Starts = row.Get<DateTime?>("Starts");
            Input.Ends = row.Get<DateTime?>("Ends");
            Input.Guests = row.List("Guests");
            TryValidateModel(Input, "Input");
            return await OnPostSaveAsync();
        }, () => RedirectToPage(new { mode = "manage" }));
    }
}
