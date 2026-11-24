using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Services;

public static class ZkAttendancePush
{
    private static readonly SemaphoreSlim Uploads = new(1, 1);
    private const int MaxBytes = 256 * 1024;

    public static string? Serial(string? value) =>
        value is { Length: > 0 and <= 64 } && Regex.IsMatch(value, @"\A[A-Za-z0-9_-]+\z")
            ? value.ToUpperInvariant() : null;

    public static string? LocalAddress(IPAddress? address, bool allowLoopback)
    {
        if (address == null) return null;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return allowLoopback ? address.ToString() : null;
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && (bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
            bytes[0] == 192 && bytes[1] == 168) ? address.ToString() : null;
    }

    public static List<(string Pin, DateTime Time)>? Parse(string body, DateTime localNow)
    {
        var rows = new List<(string, DateTime)>();
        foreach (var line in body.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = line.TrimEnd('\r').Split('\t');
            if (rows.Count == 2000 || fields.Length < 2 || fields[0].Length is < 1 or > 64 ||
                !Regex.IsMatch(fields[0], @"\A[A-Za-z0-9._-]+\z") ||
                !DateTime.TryParseExact(fields[1], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date) || date.Year < 2000 || date > localNow.AddMinutes(15))
                return null;
            rows.Add((fields[0], date));
        }
        return rows.Count == 0 ? null : rows;
    }

    private static IResult Reply(string text) => Results.Text(text, "text/plain", Encoding.ASCII);
    private static async Task<ZkDevice?> Device(HttpContext context, HrDbContext db, IWebHostEnvironment env)
    {
        var sn = Serial(context.Request.Query["SN"].ToString());
        if (context.Items[typeof(AttendanceOfficeBridge)] is AttendanceOfficeBridge.Identity connector)
            return sn == null ? null : await db.ZkDevices.AsNoTracking().SingleOrDefaultAsync(d => d.Id == connector.DeviceId &&
                d.OfficeConnectorKeyHash == connector.Hash && d.PushSerial == sn && d.PushAddress == connector.Address, context.RequestAborted);
        var address = LocalAddress(context.Connection.RemoteIpAddress, env.IsDevelopment());
        if (sn == null || address == null) return null;
        return await db.ZkDevices.AsNoTracking().SingleOrDefaultAsync(d => d.PushSerial == sn && d.PushAddress == address, context.RequestAborted);
    }

    public static async Task<IResult> Poll(HttpContext context, HrDbContext db, IWebHostEnvironment env)
    {
        var device = await Device(context, db, env);
        if (device == null) return Results.NotFound();
        var now = DateTime.UtcNow;
        var earlier = now.AddSeconds(-30);
        await db.ZkDevices.Where(d => d.Id == device.Id && d.PushSerial == device.PushSerial && d.PushAddress == device.PushAddress &&
                (d.LastPushSeenUtc == null || d.LastPushSeenUtc < earlier))
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastPushSeenUtc, now), context.RequestAborted);
        if (!context.Request.Query.ContainsKey("options")) return Reply("OK");
        var stamp = device.PushStamp ?? "0";
        return Reply($"GET OPTION FROM: {context.Request.Query["SN"]}\r\nStamp={stamp}\r\nATTLOGStamp={stamp}\r\nOpStamp=0\r\nErrorDelay=30\r\nDelay=15\r\nTransInterval=1\r\nTransFlag=1000000000\r\nRealtime=1\r\nEncrypt=0\r\n");
    }

    private static IQueryable<ZkDeviceCommand> Commands(HrDbContext db, ZkDevice device) =>
        db.ZkDeviceCommands.Where(c => c.DeviceId == device.Id && c.Serial == device.PushSerial && c.Address == device.PushAddress);

    public static async Task<IResult> NextCommand(HttpContext context, HrDbContext db, IWebHostEnvironment env)
    {
        var poll = await Poll(context, db, env);
        var device = await Device(context, db, env);
        if (device == null) return poll;

        var ct = context.RequestAborted;
        var now = DateTime.UtcNow;
        var commands = Commands(db, device);
        var command = await commands.Where(c => c.Result == null && c.ExpiresUtc > now)
            .OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        if (command == null || command.SentUtc != null) return poll;
        var claimed = await commands.Where(c => c.Id == command.Id && c.SentUtc == null && c.Result == null && c.ExpiresUtc > now &&
            db.ZkDevices.Any(d => d.Id == device.Id && d.PushSerial == command.Serial && d.PushAddress == command.Address))
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.SentUtc, now).SetProperty(c => c.ExpiresUtc, now.AddMinutes(3)), ct);
        if (claimed == 0) return poll;

        var payload = command.FingerSlot is int slot
            ? $"ENROLL_FP PIN={command.Pin}\tFID={slot}\tRETRY=3\tOVERWRITE=0"
            : $"DATA UPDATE USERINFO PIN={command.Pin}\tName={command.Name}\tPri=0";
        return Results.Text($"C:{command.Id}:{payload}\r\n", "text/plain", Encoding.UTF8);
    }

    public static async Task<IResult> CommandResult(HttpContext context, HrDbContext db, IWebHostEnvironment env)
    {
        var device = await Device(context, db, env);
        if (device == null) return Results.NotFound();
        var ct = context.RequestAborted;
        var buffer = new byte[4097];
        var length = await context.Request.Body.ReadAtLeastAsync(buffer, buffer.Length, false, ct);
        if (length == buffer.Length) return Results.StatusCode(413);
        var body = Encoding.ASCII.GetString(buffer, 0, length);
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(line.Trim());
            if (!fields.TryGetValue("ID", out var idText) || !int.TryParse(idText, out var id) ||
                !fields.TryGetValue("Return", out var resultText) || !int.TryParse(resultText, out var result))
                continue;
            await Commands(db, device).Where(c => c.Id == id && c.SentUtc != null && c.Result == null)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.Result, result), ct);
        }
        return Reply("OK");
    }

    public static async Task<IResult> Upload(HttpContext context, HrDbContext db, IWebHostEnvironment env, AttendanceService attendance)
    {
        var device = await Device(context, db, env);
        if (device == null) return Results.NotFound();
        var request = context.Request;
        var stamp = request.Query["Stamp"].ToString();
        var table = request.Query["table"].ToString();
        var stampValid = Regex.IsMatch(stamp, @"\A[0-9]{1,32}\z");
        if (!(table.Equals("ATTLOG", StringComparison.OrdinalIgnoreCase) || table.Length == 0 && stampValid &&
            !request.Query.ContainsKey("OpStamp") && !request.Query.ContainsKey("PhotoStamp")))
            return Results.StatusCode(415);
        if (request.ContentLength > MaxBytes) return Results.StatusCode(413);
        var ct = context.RequestAborted;
        var bytes = new byte[MaxBytes + 1];
        var length = 0;
        while (length < bytes.Length)
        {
            var read = await request.Body.ReadAsync(bytes.AsMemory(length), ct);
            if (read == 0) break;
            length += read;
        }
        if (length > MaxBytes) return Results.StatusCode(413);
        List<(string Pin, DateTime Time)>? rows;
        try { rows = Parse(new UTF8Encoding(false, true).GetString(bytes, 0, length).TrimStart('\uFEFF'), BusinessClock.Now); }
        catch (DecoderFallbackException) { return Results.BadRequest(); }
        if (rows == null) return Results.BadRequest();
        if (!await Uploads.WaitAsync(TimeSpan.FromSeconds(4), ct)) return Results.StatusCode(503);
        try
        {
            var current = await db.ZkDevices.SingleOrDefaultAsync(d => d.Id == device.Id && d.PushSerial == device.PushSerial && d.PushAddress == device.PushAddress, ct);
            if (current == null) return Results.NotFound();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var pins = rows.Select(r => r.Pin).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var first = rows.Min(r => r.Time);
            var last = rows.Max(r => r.Time);
            var existing = await db.ZkPunchs.AsNoTracking().Where(p => p.DeviceId == current.Id && p.PunchTime >= first &&
                p.PunchTime <= last && pins.Contains(p.Pin)).Select(p => new { p.Pin, p.PunchTime }).ToListAsync(ct);
            var seen = existing.Select(p => (p.Pin.ToUpperInvariant(), p.PunchTime)).ToHashSet();
            var unique = rows.Where(r => seen.Add((r.Pin.ToUpperInvariant(), r.Time))).ToList();
            foreach (var row in unique)
                db.ZkPunchs.Add(new() { DeviceId = current.Id, Pin = row.Pin, PunchTime = row.Time });
            if (unique.Count != 0)
                db.AuditLogs.Add(new() { Actor = "Attendance device", Action = $"Automatic push: device {current.Id}; {unique.Count} new punches" });
            await db.SaveChangesAsync(ct);
            var employees = await db.Employees.Where(e => e.ZkPin != null && pins.Contains(e.ZkPin)).Select(e => new { e.Id, e.ZkPin }).ToListAsync(ct);
            var map = employees.ToDictionary(e => e.ZkPin!, e => e.Id, StringComparer.OrdinalIgnoreCase);
            var days = new HashSet<(int Employee, DateTime Day)>();
            var incomplete = false;
            foreach (var row in unique)
            {
                if (!map.TryGetValue(row.Pin, out var employee)) { incomplete = true; continue; }
                try { days.Add((employee, await attendance.WorkDateAsync(employee, row.Time))); }
                catch (InvalidOperationException ex) when (MissingSchedule(ex)) { incomplete = true; }
            }
            foreach (var day in days)
            {
                try { await attendance.RebuildAsync(day.Employee, day.Day); }
                catch (InvalidOperationException ex) when (MissingSchedule(ex)) { incomplete = true; }
            }
            current.LastPushSeenUtc = current.LastSuccess = DateTime.UtcNow;
            if (unique.Count != 0)
                current.LastError = incomplete ? "Punches saved; map missing PINs or configure work rules and rebuild attendance." : null;
            if (stampValid && (current.PushStamp == null ||
                System.Numerics.BigInteger.Parse(stamp, CultureInfo.InvariantCulture) >= System.Numerics.BigInteger.Parse(current.PushStamp, CultureInfo.InvariantCulture)))
                current.PushStamp = stamp;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Reply(table.Length == 0 ? "OK" : $"OK: {rows.Count}");
        }
        finally { Uploads.Release(); }
    }

    private static bool MissingSchedule(InvalidOperationException ex) =>
        ex.Message.StartsWith("No effective assignment for employee ", StringComparison.Ordinal) ||
        ex.Message == "Configure an effective work rule first.";
}
