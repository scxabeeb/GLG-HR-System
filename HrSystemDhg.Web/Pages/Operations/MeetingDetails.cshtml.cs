using System.ComponentModel.DataAnnotations;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Operations;

public class MeetingDetailsModel(HrDbContext db, OperationsNotices notices, ModuleAccess access, IWebHostEnvironment env, IConfiguration configuration) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty, StringLength(12000)] public string? Notes { get; set; }
    public WorkMeeting Meeting { get; private set; } = null!;
    public int AttendingCount { get; private set; }
    public int InvitedCount { get; private set; }
    public DateTime? MyAttendance { get; private set; }
    public bool CanCheckIn { get; private set; }
    public List<AttendanceRow> AttendanceList { get; private set; } = [];
    public record AttendanceRow(string Name, DateTime? Time);

    public bool CanAddAgendaItem => !Meeting.Cancelled && Meeting.EndedUtc == null && Meeting.EndsUtc > DateTime.UtcNow;
    public List<AgendaRow> AgendaItems { get; private set; } = [];
    public record AgendaRow(MeetingAgendaItem Item, string Author);
    public bool CanEdit { get; private set; }
    public bool CanCreateTask { get; private set; }
    public TaskAction FollowUp { get; private set; } = new();
    public Dictionary<string, string> Assignees { get; private set; } = [];
    public Dictionary<string, string> TaskOwners { get; private set; } = [];
    public List<WorkTask> FollowUps { get; private set; } = [];

    private const long MaxDocumentBytes = 20 * 1024 * 1024;
    private static readonly SemaphoreSlim DocumentStorage = new(1);
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".csv", ".png", ".jpg", ".jpeg" };
    private string DocumentDirectory => Path.Combine(env.ContentRootPath, "App_Data", "meeting-documents",
        Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public sealed record MeetingDocument(string Key, string Name);
    public List<MeetingDocument> Documents { get; private set; } = [];
    public List<MeetingRecordingsModel.Recording> Recordings { get; private set; } = [];
    public bool CanViewRecordings => Meeting.OwnerId == db.ActorId;

    private IEnumerable<MeetingDocument> SavedDocuments()
    {
        if (!Directory.Exists(DocumentDirectory)) yield break;
        foreach (var path in Directory.EnumerateFiles(DocumentDirectory))
        {
            var name = Path.GetFileName(path);
            if (name.Length > 33 && name[32] == '-' && Guid.TryParseExact(name[..32], "N", out _) &&
                DocumentExtensions.Contains(Path.GetExtension(name)))
                yield return new(name[..32], name[33..]);
        }
    }

    public async Task<IActionResult> OnPostDocumentAsync(IFormFile? document)
    {
        if (!await Load()) return NotFound();
        if (!CanEdit) return Forbid();

        var name = Path.GetFileName(document?.FileName.Replace('\\', '/'));
        if (document == null || document.Length is <= 0 or > MaxDocumentBytes ||
            string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl) || System.Text.Encoding.UTF8.GetByteCount(name) > 180 ||
            !DocumentExtensions.Contains(Path.GetExtension(name)))
        {
            TempData["OperationsNotice"] = "Choose a supported document or image up to 20 MB.";
            return RedirectToPage("./MeetingDetails", null, new { id = Id }, "meeting-documents");
        }

        var cancellation = HttpContext.RequestAborted;
        await DocumentStorage.WaitAsync(cancellation);
        try
        {
            if (SavedDocuments().Count() >= 20)
                TempData["OperationsNotice"] = "This meeting already has 20 documents.";
            else
            {
                Directory.CreateDirectory(DocumentDirectory);
                var key = Guid.NewGuid().ToString("N");
                var temp = Path.Combine(DocumentDirectory, key + ".upload");
                try
                {
                    await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                        await document.CopyToAsync(output, cancellation);
                    System.IO.File.Move(temp, Path.Combine(DocumentDirectory, $"{key}-{name}"));
                    TempData["OperationsNotice"] = "Document uploaded for invited employees.";
                }
                finally { System.IO.File.Delete(temp); }
            }
        }
        finally { DocumentStorage.Release(); }
        return RedirectToPage("./MeetingDetails", null, new { id = Id }, "meeting-documents");
    }

    public async Task<IActionResult> OnGetDocumentAsync(string key)
    {
        if (!Guid.TryParseExact(key, "N", out _) ||
            !await OperationsPolicy.Meetings(db, db.ActorId!).AnyAsync(m => m.Id == Id)) return NotFound();
        var document = SavedDocuments().FirstOrDefault(item => item.Key == key);
        if (document == null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox";
        return PhysicalFile(Path.Combine(DocumentDirectory, $"{document.Key}-{document.Name}"),
            "application/octet-stream", document.Name);
    }

    public class TaskAction
    {
        [Required, StringLength(160)] public string Title { get; set; } = "";
        [StringLength(2500)] public string? Details { get; set; }
        [Required, StringLength(255)] public string Assignee { get; set; } = "";
        public DateTime? DueDate { get; set; }
        [EnumDataType(typeof(TaskPriority))] public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    }

    private async Task<bool> Load()
    {
        var actor = db.ActorId!;
        var item = await OperationsPolicy.Meetings(db, actor).Include(m => m.Participants).SingleOrDefaultAsync(m => m.Id == Id);
        if (item == null) return false;
        Meeting = item;
        AgendaItems = await db.MeetingAgendaItems.AsNoTracking().Where(x => x.MeetingId == Id).OrderBy(x => x.Id)
            .Select(x => new AgendaRow(x, (from user in db.Users
                join employee in db.Employees.IgnoreQueryFilters() on user.EmployeeId equals (int?)employee.Id
                where user.Id == x.AuthorId select employee.FullName).FirstOrDefault() ?? "Meeting contributor"))
            .ToListAsync();
        CanEdit = db.IsStaff && item.OwnerId == actor && !item.Cancelled;
        if (ModuleAccess.Allows(await access.BlockedAsync(), "tasks"))
        {
            CanCreateTask = CanEdit;
            FollowUps = await OperationsPolicy.Tasks(db, actor).AsNoTracking().Where(t => t.SourceMeetingId == Id)
                .OrderByDescending(t => t.Id).Take(100).ToListAsync();
            var ids = FollowUps.Select(t => t.AssigneeId).Distinct().ToArray();
            TaskOwners = await db.Users.Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? "Employee");
            if (CanCreateTask)
                Assignees = await OperationsPolicy.EligiblePeople(db, "tasks").OrderBy(u => u.Email)
                    .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? "Employee");
        }
        Documents = SavedDocuments().OrderBy(item => item.Name).ToList();
        if (CanViewRecordings)
            Recordings = MeetingRecordingsModel.ListSaved(Id, Meeting.Title, env, configuration)
                .OrderByDescending(item => item.SavedUtc).ToList();
        Response.Headers.CacheControl = "no-store";
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await Load()) return NotFound();
        if (CanEdit) Notes = Meeting.MinutesDraft;
        var participants = await db.Set<MeetingParticipant>().AsNoTracking().Where(p => p.MeetingId == Id).ToListAsync();
        InvitedCount = participants.Count;
        AttendingCount = participants.Count(p => p.AttendedUtc != null);
        var mine = participants.SingleOrDefault(p => p.UserId == db.ActorId);
        MyAttendance = mine?.AttendedUtc;
        CanCheckIn = mine != null && CanAddAgendaItem;
        if (CanEdit)
        {
            var ids = participants.Select(p => p.UserId).ToArray();
            var names = await db.Users.Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, Name = db.Employees.Where(e => e.Id == u.EmployeeId)
                    .Select(e => e.FullName).FirstOrDefault() ?? u.Email ?? u.UserName ?? "Employee" })
                .ToDictionaryAsync(u => u.Id, u => u.Name);
            AttendanceList = participants.Select(p => new AttendanceRow(names.GetValueOrDefault(p.UserId, "Employee"), p.AttendedUtc))
                .OrderBy(p => p.Time == null).ThenBy(p => p.Name).ToList();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAttendAsync()
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var meeting = await OperationsPolicy.Meetings(db, db.ActorId!).SingleOrDefaultAsync(m => m.Id == Id);
        if (meeting == null) return NotFound();
        if (meeting.Cancelled || meeting.EndedUtc != null || meeting.EndsUtc <= DateTime.UtcNow)
            return BadRequest("Attendance is closed for this meeting.");
        var participant = await db.Set<MeetingParticipant>().SingleOrDefaultAsync(p => p.MeetingId == Id && p.UserId == db.ActorId);
        if (participant == null) return Forbid();
        if (participant.AttendedUtc == null)
        {
            participant.AttendedUtc = DateTime.UtcNow;
            db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Checked in to meeting {Id}" });
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        TempData["OperationsNotice"] = "Your meeting attendance is recorded.";
        return RedirectToPage(null, null, new { id = Id }, "meeting-attendance");
    }

    public async Task<IActionResult> OnPostAgendaAsync(string? title, string? notes)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var actor = db.ActorId!;
        var meeting = await OperationsPolicy.Meetings(db, actor).AsNoTracking().SingleOrDefaultAsync(x => x.Id == Id);
        if (meeting == null) return NotFound();
        if (meeting.Cancelled || meeting.EndedUtc != null || meeting.EndsUtc <= DateTime.UtcNow)
            return BadRequest("This meeting is closed to new agenda items.");
        title = title?.Trim();
        notes = notes?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 160 || notes?.Length > 2000)
        {
            TempData["OperationsNotice"] = "Enter an agenda title up to 160 characters and notes up to 2,000 characters.";
            return RedirectToPage(new { id = Id });
        }

        db.MeetingAgendaItems.Add(new() { MeetingId = Id, AuthorId = actor, Title = title, Notes = notes });
        db.AuditLogs.Add(new() { Actor = actor, Action = $"Meeting {Id}: added shared agenda item" });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["OperationsNotice"] = "Item added to the shared meeting agenda.";
        return RedirectToPage(null, null, new { id = Id }, "meeting-agenda");
    }

    public async Task<IActionResult> OnPostAsync(bool publish)
    {
        if (!await Load()) return NotFound();
        if (!CanEdit) return Forbid();
        var notes = Notes?.Trim();
        if (publish && string.IsNullOrWhiteSpace(notes))
            ModelState.AddModelError(nameof(Notes), "Enter minutes before publishing.");
        if (!ModelState.IsValid) return Page();

        Meeting.MinutesDraft = notes;
        if (publish && Meeting.PublishedMinutes != notes)
        {
            Meeting.PublishedMinutes = notes;
            Meeting.MinutesPublishedUtc = DateTime.UtcNow;
            await notices.AddAsync(Meeting.Participants.Select(p => p.UserId), OperationsNoticeKind.MeetingMinutesPublished, meeting: Meeting);
        }
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Meeting {Id}: {(publish ? "published minutes" : "saved minutes draft")}" });
        await db.SaveChangesAsync();
        TempData["OperationsNotice"] = publish ? "Minutes published for invited employees." : "Private draft saved.";
        return RedirectToPage(new { id = Id });
    }
    public async Task<IActionResult> OnPostFollowUpAsync([Bind(Prefix = "FollowUp")] TaskAction input)
    {
        if (!await Load()) return NotFound();
        if (!CanCreateTask) return Forbid();
        FollowUp = input;
        Notes = Meeting.MinutesDraft;
        if (input.Assignee == null || !Assignees.ContainsKey(input.Assignee))
            ModelState.AddModelError("FollowUp.Assignee", "Select an active employee account with task access in your branch scope.");
        if (input.DueDate?.Year < 1900)
            ModelState.AddModelError("FollowUp.DueDate", "Select a valid due date.");
        if (!ModelState.IsValid) return Page();

        var actor = db.ActorId!;
        var now = DateTime.UtcNow;
        var task = new WorkTask
        {
            Title = input.Title.Trim(), Description = input.Details?.Trim() ?? "", OwnerId = actor,
            AssigneeId = input.Assignee!, DueDate = input.DueDate?.Date, Priority = input.Priority,
            UpdatedUtc = now, SourceMeetingId = Id
        };
        db.WorkTasks.Add(task);
        db.TaskActivities.Add(new() { Task = task, ActorId = actor, Action = "Created from meeting", NewStatus = task.Status, RecordedUtc = now });
        db.AuditLogs.Add(new() { Actor = actor, Action = $"Created a follow-up task from meeting {Id}" });
        if (task.AssigneeId != actor) await notices.AddTaskAssignmentsAsync([task]);
        await db.SaveChangesAsync();
        TempData["OperationsNotice"] = "Follow-up task assigned.";
        return RedirectToPage("./MeetingDetails", null, new { id = Id }, "meeting-follow-ups");
    }
}
