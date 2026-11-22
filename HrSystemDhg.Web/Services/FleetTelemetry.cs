using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Net;
using System.Text.Json;

namespace HrSystemDhg.Web.Services;
public static class FleetTelemetry
{
    public static async Task<IResult> Receive(int vehicleId, HttpContext context, HrDbContext db, IWebHostEnvironment environment, CancellationToken ct)
    {
        var request = context.Request;
        if (!request.IsHttps && !(environment.IsDevelopment() && context.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip)))
            return Results.BadRequest(new { error = "HTTPS is required." });
        var authorization = request.Headers.Authorization.ToString();
        if (authorization.Length != 71 || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Results.Unauthorized();
        var vehicle = await db.FleetVehicles.AsNoTracking().SingleOrDefaultAsync(v => v.Id == vehicleId, ct);
        if (vehicle == null || vehicle.Status == VehicleState.Retired || !OperationsPolicy.MatchesTrackerKey(vehicle.TrackerKeyHash, authorization[7..]))
            return Results.Unauthorized();
        if (!request.HasJsonContentType()) return Results.StatusCode(415);
        if (request.ContentLength > 4096) return Results.StatusCode(413);
        var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = 4096;
        GpsFix? fix;
        try { fix = await request.ReadFromJsonAsync<GpsFix>(cancellationToken: ct); }
        catch (JsonException) { return Results.BadRequest(new { error = "Invalid JSON." }); }
        if (fix == null || !OperationsPolicy.ValidFix(fix, DateTimeOffset.UtcNow))
            return Results.BadRequest(new { error = "Supply valid coordinates, recordedAt, and optional speedKph." });
        db.FleetPositions.Add(new FleetPosition
        {
            VehicleId = vehicleId, Latitude = fix.Latitude!.Value, Longitude = fix.Longitude!.Value,
            RecordedUtc = fix.RecordedAt!.Value.UtcDateTime, SpeedKph = fix.SpeedKph
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        { return Results.Ok(new { accepted = true, duplicate = true }); }
        return Results.Ok(new { accepted = true });
    }
}
