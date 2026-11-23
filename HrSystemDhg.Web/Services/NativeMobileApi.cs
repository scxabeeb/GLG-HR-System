using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;

namespace HrSystemDhg.Web.Services;

public static class NativeMobileApi
{
    public const string Scheme = "MobileBearer";
    public static AuthorizationPolicy Policy => new AuthorizationPolicyBuilder(Scheme).RequireAuthenticatedUser().Build();
    public static AuthorizationPolicy LegacyPolicy => new AuthorizationPolicyBuilder(IdentityConstants.ApplicationScheme, Scheme).RequireAuthenticatedUser().Build();
    public sealed record LoginInput(string Username, string Password);
    public sealed record PasswordInput(string Current, string Password);
    public sealed record RecoveryInput(string Email);
    public sealed record ResetInput(string Email, string Code, string Password);
    public sealed record StatusInput(string Status);
    public sealed record AgendaInput(string Title, string? Notes);

    private static IResult Error(string message, int status = 400) => Results.Json(new { message }, statusCode: status);

    public static void Map(WebApplication app)
    {
        var auth = app.MapGroup("/api/mobile/v2/auth");
        auth.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting("password-reset");
        auth.MapPost("/forgot", async (RecoveryInput input, PasswordRecoveryService service) =>
        {
            if (!new EmailAddressAttribute().IsValid(input.Email)) return Error("Enter your registered email address.");
            await service.RequestAsync(input.Email);
            return Results.Ok(new { message = "If the account is available, password reset instructions will be sent to its registered email." });
        }).AllowAnonymous().RequireRateLimiting("password-reset");
        auth.MapPost("/reset", async (ResetInput input, PasswordRecoveryService service) =>
        {
            if (string.IsNullOrEmpty(input.Password) || input.Password.Length is < 10 or > 128 || string.IsNullOrEmpty(input.Code) || input.Code.Length > 4096 || !new EmailAddressAttribute().IsValid(input.Email)) return Error("Enter a valid reset code and a password of 10–128 characters.");
            var result = await service.ResetAsync(input.Email, input.Code, input.Password);
            return result.Succeeded ? Results.Ok(new { message = "Password updated. Sign in with your new password." }) : Error(string.Join(" ", result.Errors.Select(e => e.Description)));
        }).AllowAnonymous().RequireRateLimiting("password-reset");
        auth.MapPost("/password", ChangePassword).RequireAuthorization(Policy);
        auth.MapPost("/logout", async (HttpContext ctx, UserManager<ApplicationUser> users) =>
        {
            var user = await users.GetUserAsync(ctx.User);
            if (user != null) await users.UpdateSecurityStampAsync(user);
            return Results.Ok(new { message = "Signed out." });
        }).RequireAuthorization(Policy);
        auth.MapGet("/me", async (HttpContext ctx, UserManager<ApplicationUser> users) =>
        {
            var user = await users.GetUserAsync(ctx.User);
            return user == null ? Results.Unauthorized() : Results.Ok(new { user.Email, user.MustChangePassword });
        }).RequireAuthorization(Policy);

        var api = app.MapGroup("/api/mobile/v2").RequireAuthorization(Policy);
        api.MapGet("/profile", Profile);
        api.MapPost("/profile/photo", Photo).DisableAntiforgery();
        api.MapGet("/tasks", Tasks);
        api.MapPost("/tasks/{id:int}/status", TaskStatus);
        api.MapGet("/meetings", Meetings);
        api.MapGet("/meetings/{id:int}", Meeting);
        api.MapPost("/meetings/{id:int}/agenda", Agenda);
        api.MapPost("/meetings/{id:int}/attend", Attend);
        api.MapPost("/meetings/{id:int}/end", End);
        api.MapGet("/meetings/{id:int}/room", Room);
        api.MapGet("/notifications", Notifications);
        api.MapPost("/notifications/{id:long}/read", ReadNotification);
        api.MapPost("/notifications/read-all", ReadAll);
        api.MapGet("/payroll", Payroll);
        api.MapGet("/payroll/{id:int}", Payslip);
        // Dedicated bearer-authenticated signaling route uses the same validated room tickets.
        app.MapGet("/api/mobile/v2/signal", (HttpContext ctx, HrDbContext db, MeetingRooms rooms, RoomLinks links) =>
            rooms.Handle(ctx, db, links)).RequireAuthorization(Policy).RequireRateLimiting("meeting-join");
    }

    private static async Task<IResult> Login(LoginInput input, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, HrDbContext db)
    {
        if (string.IsNullOrWhiteSpace(input.Username) || input.Username.Length > 256 || string.IsNullOrEmpty(input.Password) || input.Password.Length > 128)
            return Error("Enter your email or employee number and password.");
        var username = input.Username.Trim();
        var user = await users.FindByNameAsync(username) ?? await users.FindByEmailAsync(username);
        if (user == null)
        {
            var id = await db.Employees.Where(e => e.EmployeeNumber == username).Select(e => (int?)e.Id).FirstOrDefaultAsync();
            if (id != null) user = await users.Users.FirstOrDefaultAsync(u => u.EmployeeId == id);
        }
        if (user == null || !user.IsEnabled) return Error("Sign in failed. Check your credentials.", 401);
        Microsoft.AspNetCore.Identity.SignInResult check;
        try { check = await signIn.CheckPasswordSignInAsync(user, input.Password, true); }
        catch (FormatException) { return Error("Your account needs a password reset.", 401); }
        if (!check.Succeeded) return Error("Sign in failed. Check your credentials or try again later.", 401);
        return Results.SignIn(await signIn.CreateUserPrincipalAsync(user), authenticationScheme: Scheme);
    }

    private static async Task<IResult> ChangePassword(PasswordInput input, HttpContext ctx, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
    {
        if (string.IsNullOrEmpty(input.Current) || string.IsNullOrEmpty(input.Password) || input.Password.Length is < 10 or > 128) return Error("Use a password of 10–128 characters.");
        var user = await users.GetUserAsync(ctx.User);
        if (user == null) return Results.Unauthorized();
        var result = await users.ChangePasswordAsync(user, input.Current, input.Password);
        if (!result.Succeeded) return Error(string.Join(" ", result.Errors.Select(e => e.Description)));
        user.MustChangePassword = false;
        await users.UpdateAsync(user);
        return Results.SignIn(await signIn.CreateUserPrincipalAsync(user), authenticationScheme: Scheme);
    }

    private static async Task<bool> Allowed(ModuleAccess access, string module) => ModuleAccess.Allows(await access.BlockedAsync(), module);
    private static Task<int?> Own(HrDbContext db) => db.Users.Where(u => u.Id == db.ActorId).Select(u => u.EmployeeId).SingleOrDefaultAsync();

    private static async Task<IResult> Profile(HrDbContext db, ModuleAccess access)
    {
        if (!await Allowed(access, "personal")) return Results.Forbid();
        var id = await Own(db);
        var user = await db.Users.Where(u => u.Id == db.ActorId).Select(u => new { u.Email, u.PhoneNumber }).SingleAsync();
        var employee = id == null ? null : await db.Employees.AsNoTracking().Where(e => e.Id == id)
            .Select(e => new { e.FullName, e.EmployeeNumber, e.WorkEmail, e.Status, e.HireDate }).SingleOrDefaultAsync();
        return Results.Ok(new { user.Email, user.PhoneNumber, Employee = employee });
    }

    private static async Task<IResult> Photo(HttpContext ctx, HrDbContext db, ModuleAccess access, DocumentService documents)
    {
        if (!await Allowed(access, "personal")) return Results.Forbid();
        var id = await Own(db);
        if (id == null) return Error("Ask HR to link your employee profile.");
        if (!ctx.Request.HasFormContentType) return Error("Select a photo to upload.");
        var form = await ctx.Request.ReadFormAsync();
        var file = form.Files.GetFile("photo");
        if (file == null || Path.GetExtension(file.FileName).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png"))
            return Error("Select a JPG or PNG photo.");
        try { await documents.StoreAsync(id.Value, file, "Photo", null, db.ActorId!); }
        catch (InvalidOperationException e) { return Error(e.Message); }
        return Results.Ok(new { message = "Photo updated." });
    }

    private static async Task<IResult> Tasks(HrDbContext db, ModuleAccess access)
    {
        if (!await Allowed(access, "tasks")) return Results.Forbid();
        return Results.Ok(await db.WorkTasks.AsNoTracking().Where(t => db.IsAdmin || t.AssigneeId == db.ActorId || t.OwnerId == db.ActorId)
            .OrderBy(t => t.Status >= TaskProgress.Done).ThenBy(t => t.DueDate == null).ThenBy(t => t.DueDate).Take(500)
            .Select(t => new { t.Id, t.Title, t.Responsibility, t.Description, Status = t.Status.ToString(), t.Priority, t.DueDate, t.AssigneeId, t.TeamId, CanManage = db.IsAdmin || t.OwnerId == db.ActorId, AssigneeName = db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.Email).FirstOrDefault() }).ToListAsync());
    }

    private static async Task<IResult> TaskStatus(int id, StatusInput input, HrDbContext db, ModuleAccess access, OperationsNotices notices)
    {
        if (!await Allowed(access, "tasks")) return Results.Forbid();
        if (!Enum.TryParse<TaskProgress>(input.Status, out var status) || !Enum.IsDefined(status) || status == TaskProgress.Cancelled)
            return Error("Choose Open, InProgress, or Done.");
        var task = await db.WorkTasks.SingleOrDefaultAsync(t => t.Id == id && (db.IsAdmin || t.AssigneeId == db.ActorId || t.OwnerId == db.ActorId));
        if (task == null) return Results.NotFound();
        if (task.Status == TaskProgress.Cancelled) return Error("This task is cancelled.");
        if (task.Status == status) return Results.Ok(new { message = "Status saved." });
        var previous = task.Status;
        task.Status = status;
        task.UpdatedUtc = DateTime.UtcNow;
        db.TaskActivities.Add(new() { TaskId = id, ActorId = db.ActorId!, Action = "Status changed", PreviousStatus = previous, NewStatus = status, RecordedUtc = DateTime.UtcNow });
        if (task.OwnerId != db.ActorId) await notices.AddAsync([task.OwnerId], OperationsNoticeKind.TaskProgress, task: task);
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Task {id}: {status} from mobile" });
        await db.SaveChangesAsync();
        return Results.Ok(new { message = "Status saved." });
    }

    private static IQueryable<WorkMeeting> Visible(HrDbContext db) => db.IsAdmin ? db.WorkMeetings : OperationsPolicy.Meetings(db, db.ActorId!);
    private static async Task<IResult> Meetings(HrDbContext db, ModuleAccess access)
    {
        if (!await Allowed(access, "meetings")) return Results.Forbid();
        var now = DateTime.UtcNow;
        return Results.Ok(await Visible(db).AsNoTracking().OrderBy(m => m.Cancelled || m.EndedUtc != null || m.EndsUtc <= now)
            .ThenByDescending(m => m.StartsUtc).Take(500)
            .Select(m => new { m.Id, m.Title, m.Agenda, StartsUtc = DateTime.SpecifyKind(m.StartsUtc, DateTimeKind.Utc), EndsUtc = DateTime.SpecifyKind(m.EndsUtc, DateTimeKind.Utc), m.Location, m.JoinUrl, m.Cancelled, m.EndedUtc,
                Attending = m.Participants.Count(p => p.AttendedUtc != null), Invited = m.Participants.Count(),
                CheckedIn = m.Participants.Any(p => p.UserId == db.ActorId && p.AttendedUtc != null),
                IsInvited = m.Participants.Any(p => p.UserId == db.ActorId), IsOwner = db.IsAdmin || m.OwnerId == db.ActorId }).ToListAsync());
    }

    private static async Task<IResult> Meeting(int id, HrDbContext db, ModuleAccess access)
    {
        if (!await Allowed(access, "meetings")) return Results.Forbid();
        var m = await Visible(db).AsNoTracking().SingleOrDefaultAsync(m => m.Id == id);
        if (m == null) return Results.NotFound();
        var agenda = await db.MeetingAgendaItems.AsNoTracking().Where(a => a.MeetingId == id)
            .Select(a => new { a.Id, a.Title, a.Notes }).ToListAsync();
        var attendance = await db.Set<MeetingParticipant>().Where(p => p.MeetingId == id)
            .Select(p => new { p.UserId, p.AttendedUtc, Name = db.Users.Where(u => u.Id == p.UserId).Select(u => u.Email).FirstOrDefault() }).ToListAsync();
        var mine = attendance.SingleOrDefault(a => a.UserId == db.ActorId);
        return Results.Ok(new { m.Id, m.Title, m.Agenda, m.PublishedMinutes, StartsUtc = DateTime.SpecifyKind(m.StartsUtc, DateTimeKind.Utc), EndsUtc = DateTime.SpecifyKind(m.EndsUtc, DateTimeKind.Utc), m.Location, m.JoinUrl, m.Cancelled, m.EndedUtc,
            AgendaItems = agenda, Attending = attendance.Count(a => a.AttendedUtc != null), Invited = attendance.Count,
            CheckedIn = mine?.AttendedUtc != null, Participants = attendance, IsInvited = mine != null, IsOwner = db.IsAdmin || m.OwnerId == db.ActorId });
    }

    private static async Task<IResult> Agenda(int id, AgendaInput input, HrDbContext db, ModuleAccess access)
    {
        if (!await Allowed(access, "meetings")) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 160 || input.Notes?.Length > 2000) return Error("Enter a title up to 160 characters and notes up to 2,000.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var m = await Visible(db).SingleOrDefaultAsync(m => m.Id == id);
        if (m == null) return Results.NotFound();
        if (m.Cancelled || m.EndedUtc != null || m.EndsUtc <= DateTime.UtcNow) return Error("The meeting is closed.");
        db.MeetingAgendaItems.Add(new() { MeetingId = id, AuthorId = db.ActorId!, Title = input.Title.Trim(), Notes = input.Notes?.Trim() });
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Meeting {id}: mobile agenda added" });
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return Results.Ok(new { message = "Agenda item added." });
    }

    private static async Task<IResult> Attend(int id, HrDbContext db, ModuleAccess access)
    {
        if (!await Allowed(access, "meetings")) return Results.Forbid();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var m = await Visible(db).SingleOrDefaultAsync(m => m.Id == id);
        if (m == null) return Results.NotFound();
        if (m.Cancelled || m.EndedUtc != null || m.EndsUtc <= DateTime.UtcNow) return Error("Attendance is closed.");
        var p = await db.Set<MeetingParticipant>().SingleOrDefaultAsync(p => p.MeetingId == id && p.UserId == db.ActorId);
        if (p == null) return Results.Forbid();
        if (p.AttendedUtc == null)
        {
            p.AttendedUtc = DateTime.UtcNow;
            db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Mobile check-in to meeting {id}" });
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return Results.Ok(new { message = "Attendance recorded." });
    }

    private static async Task<IResult> End(int id, HrDbContext db, ModuleAccess access, MeetingRooms rooms)
    {
        if (!await Allowed(access, "meetings")) return Results.Forbid();
        var count = await db.WorkMeetings.Where(m => m.Id == id && (db.IsAdmin || m.OwnerId == db.ActorId) && !m.Cancelled && m.EndedUtc == null && m.EndsUtc > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.EndedUtc, DateTime.UtcNow));
        if (count == 0) return Error("The meeting is closed or you are not its organizer.");
        db.AuditLogs.Add(new() { Actor = db.ActorId!, Action = $"Ended meeting {id} from mobile" });
        await db.SaveChangesAsync(); await rooms.BroadcastEndedAsync(id);
        return Results.Ok(new { message = "Meeting ended." });
    }

    private static async Task<IResult> Room(int id, HrDbContext db, ModuleAccess access, RoomLinks links, IConfiguration config)
    {
        if (!await Allowed(access, "meetings")) return Results.Forbid();
        var meeting = await Visible(db).AsNoTracking().SingleOrDefaultAsync(m => m.Id == id);
        if (meeting == null) return Results.NotFound();
        if (!RoomLinks.Matches(meeting, links.Read(links.Issue(meeting, db.ActorId))) || !RoomLinks.IsOpen(meeting, DateTime.UtcNow))
            return Error("The video room opens ten minutes before the start and closes when the meeting ends.");
        var iceUrl = config["MeetingRooms:IceUrl"];
        var ice = iceUrl != null && (iceUrl.StartsWith("stun:") || iceUrl.StartsWith("stuns:") || iceUrl.StartsWith("turn:") || iceUrl.StartsWith("turns:"))
            ? new[] { new { urls = iceUrl, username = config["MeetingRooms:IceUsername"] ?? "", credential = config["MeetingRooms:IceCredential"] ?? "" } }
            : [];
        return Results.Ok(new { Ticket = links.Issue(meeting, db.ActorId), meeting.Title, IsOwner = db.IsAdmin || meeting.OwnerId == db.ActorId, IceServers = ice });
    }

    private static async Task<IResult> Notifications(HrDbContext db, ModuleAccess access)
    {
        var query = OperationsNotices.Visible(db.OperationsNotifications.AsNoTracking().Where(n => n.RecipientId == db.ActorId), await access.BlockedAsync());
        return Results.Ok(await query.OrderByDescending(n => n.Id).Take(500)
            .Select(n => new { n.Id, n.Title, Kind = n.Kind.ToString(), n.CreatedUtc, n.ReadUtc, n.TaskId, n.MeetingId }).ToListAsync());
    }

    private static async Task<IResult> ReadNotification(long id, HrDbContext db, ModuleAccess access)
    {
        var query = OperationsNotices.Visible(db.OperationsNotifications.Where(n => n.RecipientId == db.ActorId), await access.BlockedAsync());
        await query.Where(n => n.Id == id && n.ReadUtc == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadUtc, DateTime.UtcNow));
        return Results.Ok(new { message = "Marked read." });
    }

    private static async Task<IResult> ReadAll(HrDbContext db, ModuleAccess access)
    {
        await OperationsNotices.Visible(db.OperationsNotifications.Where(n => n.RecipientId == db.ActorId), await access.BlockedAsync())
            .Where(n => n.ReadUtc == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadUtc, DateTime.UtcNow));
        return Results.Ok(new { message = "All marked read." });
    }

    private static async Task<IResult> Payroll(HrDbContext db, ModuleAccess access)
    {
        if (!await Allowed(access, "personal")) return Results.Forbid();
        var employee = await Own(db);
        return Results.Ok(await (from l in db.PayrollLines.AsNoTracking()
                                 join p in db.PayrollPeriods on l.PayrollPeriodId equals p.Id
                                 where l.EmployeeId == employee && db.Payslips.Any(s => s.PayrollLineId == l.Id && s.IsPublished)
                                 orderby p.From descending
                                 select new { l.Id, p.From, p.To, l.Currency, l.Net, l.Gross, l.AttendanceApplied, l.UnpaidLeave }).Take(500).ToListAsync());
    }

    private static async Task<IResult> Payslip(int id, HrDbContext db, ModuleAccess access)
    {
        if (!await Allowed(access, "personal")) return Results.Forbid();
        var employee = await Own(db);
        var line = await (from l in db.PayrollLines.AsNoTracking()
                          join p in db.PayrollPeriods on l.PayrollPeriodId equals p.Id
                          where l.Id == id && l.EmployeeId == employee && db.Payslips.Any(s => s.PayrollLineId == id && s.IsPublished)
                          select new { l.Id, p.From, p.To, l.Currency, l.Basic, l.AllowanceTotal, l.Overtime, l.Gross, l.AttendanceCalculated,
                              l.AttendanceApplied, l.UnpaidLeave, l.Net, l.Revision }).SingleOrDefaultAsync();
        return line == null ? Results.NotFound() : Results.Ok(line);
    }
}
