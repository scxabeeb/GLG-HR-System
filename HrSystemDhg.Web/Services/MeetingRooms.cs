using System.Net.WebSockets;
using System.Text.Json;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;

public sealed record RoomTicket(int MeetingId, long Starts, long Ends, string? UserId);
public sealed class RoomLinks(IDataProtectionProvider protection)
{
    private readonly IDataProtector protector = protection.CreateProtector("GaroweHR.MeetingRoom.v1");
    public string Issue(WorkMeeting meeting, string? userId = null) =>
        protector.Protect(JsonSerializer.Serialize(new RoomTicket(meeting.Id, meeting.StartsUtc.Ticks, meeting.EndsUtc.Ticks, userId)));

    public RoomTicket? Read(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 2048) return null;
        try { return JsonSerializer.Deserialize<RoomTicket>(protector.Unprotect(token)); }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException) { return null; }
    }

    public static bool Matches(WorkMeeting meeting, RoomTicket? ticket) =>
        ticket != null && !meeting.Cancelled && meeting.Location == null && meeting.JoinUrl.Length == 0 && ticket.MeetingId == meeting.Id &&
        ticket.Starts == meeting.StartsUtc.Ticks && ticket.Ends == meeting.EndsUtc.Ticks;

    public static bool IsOpen(WorkMeeting meeting, DateTime utc) =>
        !meeting.Cancelled && meeting.EndedUtc == null && utc >= meeting.StartsUtc.AddMinutes(-10) && utc < meeting.EndsUtc;
}

public sealed class MeetingRooms
{
    public async Task BroadcastEndedAsync(int meetingId)
    {
        Peer[] recipients;
        lock (sync)
            recipients = rooms.TryGetValue(meetingId, out var room) ? room.Values.ToArray() : [];
        await Task.WhenAll(recipients.Select(peer => peer.SendAfterWelcome(new { type = "ended" })));
    }

    private const int MaxMessage = 48 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<int, Dictionary<string, Peer>> rooms = [];
    private readonly Dictionary<int, string> recordingOwners = [];
    private readonly Dictionary<int, Whiteboard> boards = [];
    private long recordingVersion;
    private sealed class Whiteboard
    {
        public int Generation { get; set; }
        public List<int[]> Lines { get; } = [];
    }
    private readonly object sync = new();
    private sealed class Peer(WebSocket socket, string name)
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Name { get; } = name;
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task SendAfterWelcome(object message)
        {
            await Ready.Task;
            await Send(message);
        }
        private readonly SemaphoreSlim sender = new(1);
        public async Task Send(object message)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                await sender.WaitAsync(timeout.Token);
                try
                {
                    if (socket.State == WebSocketState.Open)
                        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message, Json), WebSocketMessageType.Text, true, timeout.Token);
                }
                finally { sender.Release(); }
            }
            catch (Exception e) when (e is WebSocketException or OperationCanceledException) { socket.Abort(); }
        }
    }

    private static async Task<JsonDocument?> Read(WebSocket socket, CancellationToken ct, int seconds)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        var buffer = new byte[MaxMessage];
        var used = 0;
        while (used < buffer.Length)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(used), timeout.Token);
            if (result.MessageType != WebSocketMessageType.Text) return null;
            used += result.Count;
            if (result.EndOfMessage) return JsonDocument.Parse(buffer.AsMemory(0, used));
        }
        return null;
    }

    private static string? Text(JsonElement message, string key) =>
        message.ValueKind == JsonValueKind.Object && message.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static async Task<bool> Authorized(HrDbContext db, RoomTicket? ticket, CancellationToken ct)
    {
        if (ticket == null) return false;
        var meeting = await db.WorkMeetings.AsNoTracking().SingleOrDefaultAsync(m => m.Id == ticket.MeetingId, ct);
        if (meeting == null || !RoomLinks.Matches(meeting, ticket) || !RoomLinks.IsOpen(meeting, DateTime.UtcNow)) return false;
        if (ticket.UserId == null) return true;
        return ticket.UserId == db.ActorId &&
            (db.IsAdmin || !await db.UserModuleRestrictions.AnyAsync(r => r.UserId == ticket.UserId && r.Module == "meetings", ct)) &&
            await db.Users.AnyAsync(u => u.Id == ticket.UserId && u.IsEnabled && !u.MustChangePassword, ct) &&
            await OperationsPolicy.Meetings(db, ticket.UserId).AnyAsync(m => m.Id == ticket.MeetingId, ct);
    }

    private static async Task<bool> IsOrganizer(HrDbContext db, RoomTicket ticket, CancellationToken ct) =>
        ticket.UserId != null && await db.WorkMeetings.AnyAsync(m =>
            m.Id == ticket.MeetingId && m.OwnerId == ticket.UserId && !m.Cancelled && m.EndedUtc == null && m.EndsUtc > DateTime.UtcNow, ct);

    public async Task Handle(HttpContext context, HrDbContext db, RoomLinks links)
    {
        var request = context.Request;
        var origin = $"{request.Scheme}://{request.Host}";
        var native = request.Path == "/api/mobile/v2/signal" && context.User.Identity?.IsAuthenticated == true;
        if (!context.WebSockets.IsWebSocketRequest ||
            (!native && !string.Equals(request.Headers.Origin, origin, StringComparison.OrdinalIgnoreCase)))
        { context.Response.StatusCode = 400; return; }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var ct = context.RequestAborted;
        if (context.User.HasClaim(c => c.Type == "support-admin")) return;
        Peer? peer = null;
        RoomTicket? ticket = null;
        try
        {
            using var hello = await Read(socket, ct, 8);
            if (hello == null) return;
            ticket = links.Read(Text(hello.RootElement, "ticket"));
            if (!await Authorized(db, ticket, ct))
            {
                var denied = new Peer(socket, "");
                if (ticket != null && (ticket.UserId == null || ticket.UserId == db.ActorId) &&
                    await db.WorkMeetings.AnyAsync(m => m.Id == ticket.MeetingId && m.EndedUtc != null &&
                        m.StartsUtc == new DateTime(ticket.Starts) && m.EndsUtc == new DateTime(ticket.Ends), ct))
                    await denied.Send(new { type = "ended" });
                else
                    await denied.Send(new { type = "error", message = "This invitation is unavailable or the meeting is closed." });
                return;
            }
            var id = ticket!.MeetingId;
            var name = Text(hello.RootElement, "name")?.Trim();
            var member = new Peer(socket, string.IsNullOrWhiteSpace(name) ? "Participant" : name[..Math.Min(name.Length, 50)]);
            Peer[]? others = null;
            bool recording = false;
            object? boardSnapshot = null;
            long version = 0;
            lock (sync)
            {
                if (!rooms.ContainsKey(id) && rooms.Count < 120) rooms[id] = [];
                if (rooms.TryGetValue(id, out var room) && room.Count < 6)
                {
                    others = room.Values.ToArray();
                    if (!boards.TryGetValue(id, out var board)) boards[id] = board = new();
                    boardSnapshot = new { board.Generation, lines = board.Lines.ToArray() };
                    recording = recordingOwners.ContainsKey(id);
                    version = recordingVersion;
                    room.Add(member.Id, member);
                    peer = member;
                }
            }
            if (others == null)
            {
                await member.Send(new { type = "error", message = "The room is full. Up to six people can join." });
                return;
            }
            if (await db.WorkMeetings.AnyAsync(m => m.Id == id && m.EndedUtc != null, ct))
            {
                await member.Send(new { type = "ended" });
                return;
            }
            await member.Send(new { type = "welcome", recording, version, board = boardSnapshot, peers = others.Select(p => new { id = p.Id, name = p.Name }) });
            member.Ready.TrySetResult();
            await Task.WhenAll(others.Select(p => p.SendAfterWelcome(new { type = "joined", from = member.Id, name = member.Name })));
            var lastCheck = DateTime.UtcNow;
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var message = await Read(socket, ct, 25);
                if (message == null) break;
                if (DateTime.UtcNow - lastCheck >= TimeSpan.FromSeconds(5))
                {
                    if (!await Authorized(db, ticket, ct)) { await member.Send(new { type = "error", message = "The meeting ended or access was revoked." }); break; }
                    lastCheck = DateTime.UtcNow;
                }
                var root = message.RootElement;
                var type = Text(root, "type");
                if (type == "ping") continue;
                if (type == "end-meeting")
                {
                    if (ticket.UserId == null) continue;
                    var now = DateTime.UtcNow;
                    await using var transaction = await db.Database.BeginTransactionAsync(ct);
                    var changed = await db.WorkMeetings.Where(m => m.Id == id && m.OwnerId == ticket.UserId &&
                        !m.Cancelled && m.EndedUtc == null && m.EndsUtc > now &&
                        m.StartsUtc == new DateTime(ticket.Starts) && m.EndsUtc == new DateTime(ticket.Ends))
                        .ExecuteUpdateAsync(set => set.SetProperty(m => m.EndedUtc, now), ct);
                    if (changed == 0) continue;
                    db.AuditLogs.Add(new() { Actor = ticket.UserId, Action = $"Ended meeting {id} for everyone" });
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);

                    await BroadcastEndedAsync(id);
                    break;
                }
                if (type is "board-line" or "board-clear" or "mute-all")
                {
                    if (type != "board-line" && !await IsOrganizer(db, ticket, ct)) continue;
                    int[]? line = null;
                    if (type == "board-line")
                    {
                        if (!root.TryGetProperty("line", out var points) || points.ValueKind != JsonValueKind.Array ||
                            points.GetArrayLength() != 4) continue;
                        line = new int[4];
                        var index = 0;
                        foreach (var point in points.EnumerateArray())
                        {
                            if (point.ValueKind != JsonValueKind.Number || !point.TryGetInt32(out var value) ||
                                value is < 0 or > 1000) break;
                            line[index++] = value;
                        }
                        if (index != 4) continue;
                    }

                    Peer[] recipients;
                    int generation;
                    lock (sync)
                    {
                        var board = boards[id];
                        if (line != null)
                        {
                            if (!root.TryGetProperty("generation", out var requested) ||
                                requested.ValueKind != JsonValueKind.Number || !requested.TryGetInt32(out var expected) ||
                                expected != board.Generation) continue;
                            if (board.Lines.Count >= 3000) type = "board-full";
                            else board.Lines.Add(line);
                        }
                        if (type == "board-clear")
                        {
                            board.Lines.Clear();
                            board.Generation++;
                        }
                        generation = board.Generation;
                        recipients = type == "board-full" ? [member] : rooms[id].Values.Where(p => type != "mute-all" || p != member).ToArray();
                    }
                    await Task.WhenAll(recipients.Select(p => p.SendAfterWelcome(new { type, line, generation })));
                    continue;
                }
                if (type is "record-start" or "record-stop")
                {
                    var active = type == "record-start";
                    if (!await IsOrganizer(db, ticket, ct))
                        continue;
                    Peer[] recipients;
                    lock (sync)
                    {
                        if (active && recordingOwners.ContainsKey(id) ||
                            !active && recordingOwners.GetValueOrDefault(id) != member.Id) continue;
                        if (active) recordingOwners[id] = member.Id;
                        else recordingOwners.Remove(id);
                        version = ++recordingVersion;
                        recipients = rooms[id].Values.ToArray();
                    }
                    await Task.WhenAll(recipients.Where(p => p != member).Select(p => p.Send(new { type = "recording", active, own = false, version })));
                    await member.Send(new { type = "recording", active, own = true, version });
                    continue;
                }

                var targetId = Text(root, "to");
                if (type is not ("offer" or "answer" or "ice") || targetId is not { Length: 32 } ||
                    !root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object) continue;
                Peer? target;
                lock (sync) target = rooms.GetValueOrDefault(id)?.GetValueOrDefault(targetId);
                if (target != null && target != member)
                    await target.Send(new { type, from = member.Id, name = member.Name, payload });
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or JsonException) { }
        finally
        {
            if (peer != null && ticket != null)
            {
                peer.Ready.TrySetResult();
                Peer[] remaining;
                long stoppedVersion = 0;
                lock (sync)
                {
                    var room = rooms[ticket.MeetingId];
                    room.Remove(peer.Id);
                    if (recordingOwners.GetValueOrDefault(ticket.MeetingId) == peer.Id)
                    {
                        recordingOwners.Remove(ticket.MeetingId);
                        stoppedVersion = ++recordingVersion;
                    }
                    remaining = room.Values.ToArray();
                    if (room.Count == 0)
                    {
                        rooms.Remove(ticket.MeetingId);
                        boards.Remove(ticket.MeetingId);
                    }
                }
                if (stoppedVersion != 0)
                    await Task.WhenAll(remaining.Select(p => p.Send(new { type = "recording", active = false, own = false, version = stoppedVersion })));
                await Task.WhenAll(remaining.Select(p => p.Send(new { type = "left", from = peer.Id })));
            }
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Meeting connection closed", timeout.Token); }
                catch (Exception ex) when (ex is WebSocketException or OperationCanceledException) { socket.Abort(); }
            }
        }
    }
}
