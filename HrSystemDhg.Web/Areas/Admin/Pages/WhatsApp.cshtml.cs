using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class WhatsAppModel(HrDbContext db, IConfiguration configuration, OperationsNotices notices) : PageModel
{
    public bool Enabled => configuration.GetValue<bool>("WhatsApp:Enabled");
    public bool Configured => WhatsAppSettings.From(configuration) != null;
    public string? Sender => OperationsNotices.NormalizePhone(configuration["WhatsApp:SenderNumber"]);
    public Dictionary<WhatsAppDelivery,int> Counts { get; private set; } = [];
    public bool CanSendWhatsApp => WhatsAppSettings.From(configuration)?.StaffTemplate != null;
    public List<Recipient> Recipients { get; private set; } = [];
    public List<RecentMessage> Recent { get; private set; } = [];
    public record Recipient(string Id, string Name, string Number);
    public record RecentMessage(string Text, string Recipient, DateTime CreatedUtc, WhatsAppDelivery Status);

    public async Task OnGetAsync()
    {
        Response.Headers.CacheControl = "no-store";
        Recipients = await (from user in db.Users
                            join employee in db.Employees on user.EmployeeId equals (int?)employee.Id
                            where user.IsEnabled
                            orderby employee.FullName, employee.EmployeeNumber
                            select new Recipient(user.Id, employee.FullName, employee.EmployeeNumber)).ToListAsync();
        Recent = await (from notice in db.OperationsNotifications
                        join user in db.Users on notice.RecipientId equals user.Id
                        where notice.Kind == OperationsNoticeKind.StaffMessage
                        orderby notice.Id descending
                        select new RecentMessage(notice.Title, user.Email ?? user.UserName ?? "Employee", notice.CreatedUtc, notice.WhatsApp))
            .Take(50).ToListAsync();
        Counts = await db.OperationsNotifications.Where(n => n.WhatsApp != WhatsAppDelivery.InAppOnly)
            .GroupBy(n => n.WhatsApp).Select(g => new { State = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.State, x => x.Count);
    }

    public async Task<IActionResult> OnPostSendAsync(string? recipientId, string? message, bool whatsapp)
    {
        message = message?.Trim();
        if (string.IsNullOrEmpty(message) || message.Length > 160 || message.Any(char.IsControl))
            ModelState.AddModelError("", "Enter a single-line message up to 160 characters.");

        if (!await db.Users.AnyAsync(u => u.Id == recipientId && u.IsEnabled && u.EmployeeId != null))
            ModelState.AddModelError("", "Choose an active employee account.");

        if (whatsapp && ModelState.IsValid)
        {
            var number = await notices.RegisteredNumberAsync(recipientId!);
            if (!CanSendWhatsApp)
                ModelState.AddModelError("", "Configure the approved staff message template first.");
            else if (number == null || !await db.WhatsAppPreferences.AnyAsync(p => p.UserId == recipientId && p.Number == number))
                ModelState.AddModelError("", "The employee needs a registered mobile number and WhatsApp opt-in.");
            else if (message!.Contains("     "))
                ModelState.AddModelError("", "Use single spaces in WhatsApp messages.");
        }

        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        db.OperationsNotifications.Add(new OperationsNotification
        {
            RecipientId = recipientId!, Title = message!, Kind = OperationsNoticeKind.StaffMessage,
            CreatedUtc = DateTime.UtcNow, WhatsApp = whatsapp ? WhatsAppDelivery.Queued : WhatsAppDelivery.InAppOnly
        });
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Sent staff message to account {recipientId}" });
        await db.SaveChangesAsync();
        TempData["WhatsAppMessage"] = whatsapp ? "Inbox message saved and WhatsApp queued." : "Message sent to the employee inbox.";
        return RedirectToPage();
    }
}
