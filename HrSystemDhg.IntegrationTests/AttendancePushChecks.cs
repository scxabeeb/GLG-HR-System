using System.Net;
using System.Text;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

public static class AttendancePushChecks
{
    public static async Task RunAsync(DbContextOptions<HrDbContext> options, string pin, int employee, Action<bool, string> check)
    {
        await using var db = new HrDbContext(options);
        var device = new ZkDevice { Name = "Push fixture", Endpoint = "Fixture", PushSerial = "PUSH-" + pin.ToUpperInvariant(), PushAddress = "192.168.80.50" };
        db.ZkDevices.Add(device);
        await db.SaveChangesAsync();
        var day = new DateTime(2026, 10, 20);
        var unmapped = "missing-" + pin;
        var body = $"{pin}\t2026-10-20 22:01:00\t0\t1\r\n{pin}\t2026-10-21 06:01:00\t1\t1\r\n{unmapped}\t2026-10-20 22:02:00\t0\t1\r\n";

        async Task<IResult> Send(string ip, string payload, string table = "ATTLOG")
        {
            await using var requestDb = new HrDbContext(options);
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            context.Request.QueryString = new QueryString($"?SN={device.PushSerial}&table={table}&Stamp=10");
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            return await ZkAttendancePush.Upload(context, requestDb, new EnvironmentStub(), new AttendanceService(requestDb, new CalendarService(requestDb)));
        }

        check((await Send("192.168.80.51", body) as IStatusCodeHttpResult)?.StatusCode == 404,
            "attendance push rejects a known serial from the wrong source address");
        check((await Send("192.168.80.50", body, "OPERLOG") as IStatusCodeHttpResult)?.StatusCode == 415 &&
              (await Send("192.168.80.50", body + "invalid") as IStatusCodeHttpResult)?.StatusCode == 400 &&
              !await db.ZkPunchs.AnyAsync(p => p.DeviceId == device.Id), "push rejects non-attendance and malformed batches before writing");
        await Send("192.168.80.50", body);
        await Send("192.168.80.50", body);
        check(await db.ZkPunchs.CountAsync(p => p.DeviceId == device.Id) == 3 &&
              await db.ZkPunchs.AnyAsync(p => p.DeviceId == device.Id && p.Pin == unmapped),
            "automatic upload preserves unmapped punches and deduplicates retries");
        check(await db.AttendanceDays.AnyAsync(d => d.EmployeeId == employee && d.Date == day &&
            d.FirstIn == day.AddHours(22).AddMinutes(1) && d.LastOut == day.AddDays(1).AddHours(6).AddMinutes(1)),
            "automatic punches rebuild mapped overnight attendance");
        await db.Entry(device).ReloadAsync();
        check(device.LastSuccess.HasValue && device.PushStamp == "10" && device.LastError != null, "terminal upload status records success with an unmapped warning");
    }

    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public string EnvironmentName { get; set; } = "Production";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
