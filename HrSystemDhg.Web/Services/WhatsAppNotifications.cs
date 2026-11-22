using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;

public sealed class WhatsAppSettings
{
    public required string Version { get; init; }
    public required string PhoneNumberId { get; init; }
    public required string AccessToken { get; init; }
    public required string TaskTemplate { get; init; }
    public required string MeetingTemplate { get; init; }
    public string? StaffTemplate { get; init; }
    public required string Language { get; init; }

    public static WhatsAppSettings? From(IConfiguration config)
    {
        var c = config.GetSection("WhatsApp");
        var version = c["GraphVersion"] ?? "";
        var phone = c["PhoneNumberId"] ?? "";
        var token = c["AccessToken"] ?? "";
        var task = c["TaskTemplate"] ?? "";
        var meeting = c["MeetingTemplate"] ?? "";
        var staff = c["StaffTemplate"] ?? "";
        var language = c["Language"] ?? "en_US";
        if (!c.GetValue<bool>("Enabled") || !Regex.IsMatch(version, @"\Av[0-9]{1,3}\.[0-9]{1,3}\z") ||
            !Regex.IsMatch(phone, @"\A[0-9]{5,32}\z") || string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace) ||
            !Regex.IsMatch(task, @"\A[a-z0-9_]{1,128}\z") || !Regex.IsMatch(meeting, @"\A[a-z0-9_]{1,128}\z") ||
            !Regex.IsMatch(language, @"\A[a-z]{2,3}(_[A-Z]{2})?\z")) return null;
        return new() { Version = version, PhoneNumberId = phone, AccessToken = token, TaskTemplate = task, MeetingTemplate = meeting, StaffTemplate = Regex.IsMatch(staff, @"\A[a-z0-9_]{1,128}\z") ? staff : null, Language = language };
    }

    public HttpRequestMessage CreateMessage(string number, OperationsNoticeKind kind, string? text = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{Version}/{PhoneNumberId}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        var name = kind switch
        {
            OperationsNoticeKind.TaskAssigned => TaskTemplate,
            OperationsNoticeKind.MeetingInvitation => MeetingTemplate,
            OperationsNoticeKind.StaffMessage when StaffTemplate != null && !string.IsNullOrWhiteSpace(text) => StaffTemplate,
            _ => throw new ArgumentException("No WhatsApp template configured for this message.", nameof(kind))
        };
        var language = new { code = Language };
        object template = kind == OperationsNoticeKind.StaffMessage
            ? new { name, language, components = new[] { new { type = "body", parameters = new[] { new { type = "text", text } } } } }
            : new { name, language };
        request.Content = JsonContent.Create(new
        {
            messaging_product = "whatsapp",
            to = number.TrimStart('+'),
            type = "template",
            template
        });
        return request;
    }
}

public sealed class WhatsAppWorker(IServiceScopeFactory scopes, IConfiguration configuration, IHttpClientFactory http, ILogger<WhatsAppWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(12));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var settings = WhatsAppSettings.From(configuration);
            if (settings == null) continue;
            try { await SendBatch(settings, stoppingToken); }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            { log.LogWarning("Could not process the WhatsApp notification queue."); }
        }
    }

    private async Task SendBatch(WhatsAppSettings settings, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var notices = scope.ServiceProvider.GetRequiredService<OperationsNotices>();
        var stale = DateTime.UtcNow.AddHours(-24);
        var interrupted = DateTime.UtcNow.AddMinutes(-5);
        await db.OperationsNotifications.Where(n => n.WhatsApp == WhatsAppDelivery.Sending && n.AttemptUtc < interrupted)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.WhatsApp, WhatsAppDelivery.Unknown), ct);
        await db.OperationsNotifications.Where(n => n.WhatsApp == WhatsAppDelivery.Queued && n.CreatedUtc < stale)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.WhatsApp, WhatsAppDelivery.Skipped), ct);
        var pending = await db.OperationsNotifications.AsNoTracking().Where(n => n.WhatsApp == WhatsAppDelivery.Queued)
            .OrderBy(n => n.Id).Take(20).ToListAsync(ct);
        using var client = http.CreateClient("whatsapp");
        foreach (var notice in pending)
        {
            var claimed = db.OperationsNotifications.Where(n => n.Id == notice.Id && n.WhatsApp == WhatsAppDelivery.Queued);
            var now = DateTime.UtcNow;
            if (await claimed.ExecuteUpdateAsync(s => s.SetProperty(n => n.WhatsApp, WhatsAppDelivery.Sending)
                    .SetProperty(n => n.AttemptUtc, now), ct) == 0) continue;
            var delivery = WhatsAppDelivery.Skipped;
            var number = await notices.RegisteredNumberAsync(notice.RecipientId, ct);
            var consent = number != null && await db.WhatsAppPreferences.AnyAsync(p =>
                p.UserId == notice.RecipientId && p.Number == number && p.OptedInUtc <= notice.CreatedUtc, ct);
            var current = notice.Kind switch
            {
                OperationsNoticeKind.TaskAssigned => await db.WorkTasks.AnyAsync(t =>
                    t.Id == notice.TaskId && t.AssigneeId == notice.RecipientId && t.Status < TaskProgress.Done, ct),
                OperationsNoticeKind.MeetingInvitation => await db.WorkMeetings.AnyAsync(m =>
                    m.Id == notice.MeetingId && !m.Cancelled && m.EndsUtc > now &&
                    m.Participants.Any(p => p.UserId == notice.RecipientId), ct),
                OperationsNoticeKind.StaffMessage => settings.StaffTemplate != null,
                _ => false
            };
            var module = notice.Kind switch
            {
                OperationsNoticeKind.TaskAssigned => "tasks",
                OperationsNoticeKind.MeetingInvitation => "meetings",
                _ => null
            };
            var blocked = module != null && await db.UserModuleRestrictions.AnyAsync(r => r.UserId == notice.RecipientId && r.Module == module, ct);
            if (consent && current && !blocked)
            {
                try
                {
                    using var request = settings.CreateMessage(number!, notice.Kind, notice.Title);
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                    delivery = response.IsSuccessStatusCode ? WhatsAppDelivery.Accepted : WhatsAppDelivery.Failed;
                }
                catch (Exception ex) when ((ex is HttpRequestException or OperationCanceledException) && !ct.IsCancellationRequested)
                { delivery = WhatsAppDelivery.Unknown; }
            }
            await db.OperationsNotifications.Where(n => n.Id == notice.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.WhatsApp, delivery), ct);
        }
    }
}
