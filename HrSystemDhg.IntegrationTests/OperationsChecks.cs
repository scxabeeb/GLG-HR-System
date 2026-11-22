using Microsoft.Extensions.Configuration;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

public static class OperationsChecks
{
    public static async Task RunAsync(DbContextOptions<HrDbContext> options, int branch, int otherBranch, Action<bool, string> check)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var owner = "organizer-" + suffix;
        var participant = "participant-" + suffix;
        await using var db = new HrDbContext(options);
        var task = new WorkTask { Title = "Fixture task", OwnerId = owner, AssigneeId = participant, Priority = TaskPriority.High };
        var meeting = new WorkMeeting { Title = "Fixture meeting", OwnerId = owner, JoinUrl = "https://meet.google.com/abc-defg-hij",
            StartsUtc = DateTime.UtcNow, EndsUtc = DateTime.UtcNow.AddHours(1), Participants = [new() { UserId = participant }] };
        var key = new string('F', 64);
        var vehicle = new FleetVehicle { Plate = "GPS-" + suffix, Name = "Fixture vehicle", BranchId = branch, TrackerKeyHash = OperationsPolicy.HashTrackerKey(key) };
        db.AddRange(task, meeting, vehicle);
        db.Users.AddRange(new ApplicationUser { Id = owner, UserName = owner }, new ApplicationUser { Id = participant, UserName = participant });
        db.UserBranchScopes.Add(new() { UserId = owner, BranchId = otherBranch });
        await db.SaveChangesAsync();

        check(await OperationsPolicy.Tasks(db, owner).AnyAsync(t => t.Id == task.Id) &&
            await OperationsPolicy.Tasks(db, participant).AnyAsync(t => t.Id == task.Id) &&
            !await OperationsPolicy.Tasks(db, "unrelated").AnyAsync(t => t.Id == task.Id), "task is visible only to creator and assignee");
        check(await OperationsPolicy.Meetings(db, participant).AnyAsync(m => m.Id == meeting.Id) &&
            !await OperationsPolicy.Meetings(db, "unrelated").AnyAsync(m => m.Id == meeting.Id), "only invited accounts or the organizer can see meeting links");

        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["WhatsApp:Enabled"] = "true", ["WhatsApp:GraphVersion"] = "v99.0",
                ["WhatsApp:PhoneNumberId"] = "12345", ["WhatsApp:AccessToken"] = "test-token",
                ["WhatsApp:TaskTemplate"] = "task_alert", ["WhatsApp:MeetingTemplate"] = "meeting_alert"
            }).Build();
        var notices = new OperationsNotices(db, config);
        await notices.AddAsync([participant, participant], OperationsNoticeKind.TaskAssigned, task: task);
        db.WhatsAppPreferences.Add(new() { UserId = participant, Number = "+252612345678", OptedInUtc = DateTime.UtcNow.AddMinutes(-1) });
        await db.SaveChangesAsync();
        await notices.AddAsync([participant], OperationsNoticeKind.MeetingInvitation, meeting: meeting);
        await db.SaveChangesAsync();
        check(await notices.For(participant).CountAsync() == 2 && !await notices.For(owner).AnyAsync(),
            "assignment notices are deduplicated and private to the recipient");
        check(await notices.For(participant).AnyAsync(n => n.TaskId == task.Id && n.WhatsApp == WhatsAppDelivery.InAppOnly) &&
            await notices.For(participant).AnyAsync(n => n.MeetingId == meeting.Id && n.WhatsApp == WhatsAppDelivery.Queued),
            "WhatsApp queues only opted-in invitations with sender configured");

        var actor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner), new Claim(ClaimTypes.Role, "Management")], "Test")) } };
        await using var scoped = new HrDbContext(options, actor);
        check(!await OperationsPolicy.Vehicles(scoped).AnyAsync(v => v.Id == vehicle.Id), "fleet access respects the branch scope");

        var time = DateTimeOffset.UtcNow;
        async Task<int?> Send(string secret, object data)
        {
            await using var requestDb = new HrDbContext(options);
            var http = new DefaultHttpContext();
            http.Request.Scheme = "https";
            http.Connection.RemoteIpAddress = IPAddress.Loopback;
            http.Request.ContentType = "application/json";
            http.Request.Headers.Authorization = "Bearer " + secret;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(data);
            http.Request.ContentLength = bytes.Length;
            http.Request.Body = new MemoryStream(bytes);
            return ((IStatusCodeHttpResult)await FleetTelemetry.Receive(vehicle.Id, http, requestDb, new TestEnvironment(), default)).StatusCode;
        }
        var good = new { latitude = 8.4, longitude = 48.5, recordedAt = time, speedKph = 21.0 };
        check(await Send(new string('0', 64), good) == 401, "GPS rejects an invalid bearer key");
        check(await Send(key, new { latitude = 100, longitude = 48.5, recordedAt = time }) == 400, "GPS rejects invalid coordinates");
        check(await Send(key, good) == 200 && await Send(key, good) == 200 &&
            await db.FleetPositions.CountAsync(p => p.VehicleId == vehicle.Id) == 1, "GPS persists one fix and accepts a retry without duplicating it");
        vehicle.TrackerKeyHash = null;
        await db.SaveChangesAsync();
        check(await Send(key, good) == 401, "revoked key stops tracker updates");
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DHG integration";
        public string EnvironmentName { get; set; } = "Production";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
