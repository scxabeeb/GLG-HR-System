using HrSystemDhg.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Operations;

[Authorize]
[RequestSizeLimit(105 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
public class MeetingRecordingsModel(HrDbContext db, IWebHostEnvironment env, IConfiguration configuration) : PageModel
{
    private const long MaxBytes = 100 * 1024 * 1024;
    private static readonly SemaphoreSlim Storage = new(1);
    private static string DirectoryFor(int meetingId, IWebHostEnvironment env, IConfiguration configuration) => Path.Combine(configuration["MeetingRooms:RecordingDirectory"] ?? Path.Combine(env.ContentRootPath, "App_Data", "meeting-recordings"), meetingId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private bool Allowed => db.ActorId != null && !User.HasClaim(c => c.Type == "support-admin");
    public sealed record Recording(int MeetingId, string Title, string File, DateTime SavedUtc, long Bytes);
    public List<Recording> Recordings { get; private set; } = [];

    private static bool RecordingFile(string name)
    {
        var ext = Path.GetExtension(name);
        return ext is ".webm" or ".mp4" && Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out _)
            && Path.GetFileName(name) == name;
    }

    public async Task<IActionResult> OnGetAsync(int? meetingId)
    {
        if (!Allowed) return Forbid();
        Response.Headers.CacheControl = "no-store";
        var meetings = await db.WorkMeetings.AsNoTracking().Where(m => m.OwnerId == db.ActorId &&
            (!meetingId.HasValue || m.Id == meetingId)).Select(m => new { m.Id, m.Title }).ToListAsync();
        if (meetingId.HasValue && meetings.Count == 0) return NotFound();
        foreach (var meeting in meetings)
            Recordings.AddRange(ListSaved(meeting.Id, meeting.Title, env, configuration));
        Recordings = Recordings.OrderByDescending(r => r.SavedUtc).ToList();
        return Page();
    }

    internal static IEnumerable<Recording> ListSaved(int id, string title, IWebHostEnvironment env, IConfiguration configuration)
    {
        var path = DirectoryFor(id, env, configuration);
        if (!Directory.Exists(path)) yield break;
        foreach (var file in new DirectoryInfo(path).EnumerateFiles().Where(f => RecordingFile(f.Name)))
            yield return new(id, title, file.Name, file.LastWriteTimeUtc, file.Length);
    }

    public async Task<IActionResult> OnPostSaveAsync(int meetingId, IFormFile? video)
    {
        if (!Allowed) return Forbid();
        if (!await db.WorkMeetings.AnyAsync(m => m.Id == meetingId && m.OwnerId == db.ActorId && m.Location == null && m.JoinUrl == "" &&
            m.StartsUtc.AddMinutes(-10) <= DateTime.UtcNow)) return NotFound();
        if (video == null || video.Length is < 12 or > MaxBytes) return BadRequest("Recording is empty or exceeds 100 MB.");
        await using var stream = video.OpenReadStream();
        var signature = new byte[12];
        await stream.ReadExactlyAsync(signature, HttpContext.RequestAborted);
        var extension = signature.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }) ? ".webm"
            : signature.AsSpan(4, 4).SequenceEqual("ftyp"u8) ? ".mp4" : null;
        if (extension == null) return BadRequest("Only WebM or MP4 recordings are supported.");

        var path = DirectoryFor(meetingId, env, configuration);
        await Storage.WaitAsync(HttpContext.RequestAborted);
        try
        {
            Directory.CreateDirectory(path);
            if (Directory.EnumerateFiles(path).Count(f => RecordingFile(Path.GetFileName(f))) >= 10)
                return BadRequest("This meeting already has 10 saved recordings.");
            var key = Guid.NewGuid().ToString("N");
            var temp = Path.Combine(path, key + ".upload");
            try
            {
                await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await output.WriteAsync(signature, HttpContext.RequestAborted);
                    await stream.CopyToAsync(output, HttpContext.RequestAborted);
                }
                System.IO.File.Move(temp, Path.Combine(path, key + extension));
            }
            finally { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); }
        }
        finally { Storage.Release(); }
        return new JsonResult(new { saved = true });
    }

    public async Task<IActionResult> OnGetVideoAsync(int meetingId, string file, bool download = false)
    {
        if (!Allowed) return Forbid();
        if (string.IsNullOrEmpty(file) || !RecordingFile(file) ||
            !await db.WorkMeetings.AnyAsync(m => m.Id == meetingId && m.OwnerId == db.ActorId)) return NotFound();
        var path = Path.Combine(DirectoryFor(meetingId, env, configuration), file);
        if (!System.IO.File.Exists(path)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox";
        return new PhysicalFileResult(path, file.EndsWith(".mp4", StringComparison.Ordinal) ? "video/mp4" : "video/webm")
        {
            EnableRangeProcessing = true,
            FileDownloadName = download ? $"meeting-{meetingId}-{file}" : null
        };
    }
}
