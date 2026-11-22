using System.Text.Json;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Operations;

[AllowAnonymous]
[EnableRateLimiting("meeting-join")]
public class MeetingRoomModel(HrDbContext db, RoomLinks links, IConfiguration configuration) : PageModel
{
    public WorkMeeting Meeting { get; private set; } = null!;
    public bool CanRecord { get; private set; }
    public bool Ended => Meeting.EndedUtc != null;
    public bool Open { get; private set; }
    public string Ticket { get; private set; } = "";
    public string Ice { get; private set; } = "[]";

    public async Task<IActionResult> OnGetAsync(int id, string? guest)
    {
        if (User.HasClaim(c => c.Type == "support-admin")) return Forbid();
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Frame-Options"] = "DENY";
        var meeting = await db.WorkMeetings.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id);
        if (meeting == null || meeting.Location != null || meeting.JoinUrl.Length != 0 || meeting.Cancelled || meeting.EndsUtc <= DateTime.UtcNow && meeting.EndedUtc == null) return NotFound();
        if (guest != null)
        {
            var guestTicket = links.Read(guest);
            if (guestTicket is not { UserId: null } || !RoomLinks.Matches(meeting, guestTicket)) return NotFound();
        }
        else if (db.ActorId is not { } actor || !await OperationsPolicy.Meetings(db, actor).AnyAsync(m => m.Id == id))
            return User.Identity?.IsAuthenticated == true ? NotFound() : Challenge();

        Meeting = meeting;
        CanRecord = guest == null && db.ActorId == meeting.OwnerId;
        Open = RoomLinks.IsOpen(meeting, DateTime.UtcNow);
        if (Open)
        {
            Ticket = links.Issue(meeting, guest == null ? db.ActorId : null);
            var ice = configuration["MeetingRooms:IceUrl"];
            if (ice is not null && (ice.StartsWith("stun:") || ice.StartsWith("stuns:") || ice.StartsWith("turn:") || ice.StartsWith("turns:")))
                Ice = JsonSerializer.Serialize(new[] { new { urls = ice, username = configuration["MeetingRooms:IceUsername"] ?? "", credential = configuration["MeetingRooms:IceCredential"] ?? "" } });
        }
        return Page();
    }
}
