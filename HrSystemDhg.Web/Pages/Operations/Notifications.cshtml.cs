using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Operations;

public class NotificationsModel(HrDbContext db, OperationsNotices notices, IConfiguration config, ModuleAccess access) : PageModel
{
    private string Actor => db.ActorId!;
    public List<OperationsNotification> Items { get; private set; } = [];
    public HashSet<int> JoinableRooms { get; private set; } = [];
    public bool Connected => WhatsAppSettings.From(config) != null;
    public bool CanOpenWorkspace { get; private set; }
    public bool OptedIn { get; private set; }
    public string? RegisteredNumber { get; private set; }

    public async Task OnGetAsync()
    {
        Response.Headers.CacheControl = "no-store";
        RegisteredNumber = await notices.RegisteredNumberAsync(Actor);
        OptedIn = RegisteredNumber != null && await db.WhatsAppPreferences.AnyAsync(p => p.UserId == Actor && p.Number == RegisteredNumber);
        CanOpenWorkspace = ModuleAccess.Allows(await access.BlockedAsync(), "personal");
        Items = await (await VisibleAsync()).AsNoTracking().OrderByDescending(n => n.Id).Take(100).ToListAsync();
        var ids = Items.Where(n => n.MeetingId.HasValue).Select(n => n.MeetingId!.Value).Distinct().ToArray();
        var utc = DateTime.UtcNow;
        JoinableRooms = (await OperationsPolicy.Meetings(db, Actor).Where(m => ids.Contains(m.Id) && !m.Cancelled && m.EndedUtc == null &&
            m.Location == null && m.JoinUrl == "" && m.StartsUtc <= utc.AddMinutes(10) && m.EndsUtc > utc).Select(m => m.Id).ToListAsync()).ToHashSet();
    }

    private async Task<IQueryable<OperationsNotification>> VisibleAsync() =>
        OperationsNotices.Visible(notices.For(Actor), await access.BlockedAsync());

    public async Task<IActionResult> OnGetUnreadAsync()
    {
        Response.Headers.CacheControl = "no-store";
        var count = await (await VisibleAsync()).CountAsync(n => n.ReadUtc == null);
        return new JsonResult(new { count });
    }

    public async Task<IActionResult> OnPostReadAsync(long id)
    {
        var now = DateTime.UtcNow;
        await (await VisibleAsync()).Where(n => n.Id == id && n.ReadUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadUtc, now));
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReadAllAsync()
    {
        var now = DateTime.UtcNow;
        await (await VisibleAsync()).Where(n => n.ReadUtc == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadUtc, now));
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostWhatsAppAsync(bool enable)
    {
        var preference = await db.WhatsAppPreferences.FindAsync(Actor);
        if (enable)
        {
            var number = await notices.RegisteredNumberAsync(Actor);
            if (number == null) { TempData["OperationsNotice"] = "Ask HR to add your mobile number with the country code first."; return RedirectToPage(); }
            if (preference == null)
                db.WhatsAppPreferences.Add(new() { UserId = Actor, Number = number, OptedInUtc = DateTime.UtcNow });
            else if (preference.Number != number)
            {
                preference.Number = number;
                preference.OptedInUtc = DateTime.UtcNow;
            }
        }
        else if (preference != null) db.WhatsAppPreferences.Remove(preference);
        await db.SaveChangesAsync();
        TempData["OperationsNotice"] = enable ? "WhatsApp preference saved." : "WhatsApp alerts turned off.";
        return RedirectToPage();
    }

    public static string Label(OperationsNoticeKind kind) => kind switch
    {
        OperationsNoticeKind.TaskAssigned => "Task assigned",
        OperationsNoticeKind.TaskProgress => "Task status updated",
        OperationsNoticeKind.MeetingInvitation => "Meeting invitation",
        OperationsNoticeKind.MeetingUpdated => "Meeting updated",
        OperationsNoticeKind.MeetingCancelled => "Meeting cancelled",
        OperationsNoticeKind.MeetingMinutesPublished => "Meeting minutes published",
        OperationsNoticeKind.FleetTripUpdated => "Fleet trip updated",
        OperationsNoticeKind.TeamAssigned => "Team task assigned",
        OperationsNoticeKind.AssetAssigned => "Asset assigned",
        OperationsNoticeKind.LeaveDecided => "Leave decision",
        OperationsNoticeKind.PayslipPublished => "Payslip available",
        OperationsNoticeKind.AttendanceCorrectionDecided => "Attendance correction updated",
        OperationsNoticeKind.StaffMessage => "Staff message",
        _ => "Notification"
    };
}
