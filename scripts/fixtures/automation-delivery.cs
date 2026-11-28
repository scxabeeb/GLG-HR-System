using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
var cs=Environment.GetEnvironmentVariable("AUTOMATION_FIXTURE_CONNECTION")!;
if(!cs.Contains("127.0.0.1")||!cs.Contains("native_admin_fixture"))throw new Exception("Isolated local fixture required.");
var services=new ServiceCollection();services.AddLogging();services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
var fake=new FakeClient();services.AddSingleton<IHttpClientFactory>(fake);services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
services.AddDbContext<HrDbContext>(o=>o.UseMySql(cs,new MySqlServerVersion(new Version(8,0,0))));services.AddScoped<AutomationDelivery>();
await using var provider=services.BuildServiceProvider();
using(var scope=provider.CreateScope()){
 var db=scope.ServiceProvider.GetRequiredService<HrDbContext>();var actor=await db.Users.Select(u=>u.Id).FirstAsync();var config=await db.AutomationSettings.SingleAsync();config.Tasks=true;config.Enabled=true;
 config.ProtectedWebhookSecret=scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("Garowe.Automation.Webhook.v1").Protect("fixture-password-never-log");
 await db.AutomationEvents.ExecuteDeleteAsync();await db.SaveChangesAsync();
 await using(var tx=await db.Database.BeginTransactionAsync()){
  db.WorkTasks.Add(new(){Title="Rollback task",AssigneeId=actor,OwnerId=actor});await db.SaveChangesAsync();
  if(await db.AutomationEvents.CountAsync()!=1)throw new Exception("Missing transaction event");await tx.RollbackAsync();
 }
 db.ChangeTracker.Clear();if(await db.AutomationEvents.AnyAsync()||await db.WorkTasks.AnyAsync(t=>t.Title=="Rollback task"))throw new Exception("Outbox was not atomic");
 db.WorkTasks.Add(new(){Title="Delivery test",AssigneeId=actor,OwnerId=actor});await db.SaveChangesAsync();
}
var worker=new AutomationWorker(provider.GetRequiredService<IServiceScopeFactory>(),provider.GetRequiredService<ILogger<AutomationWorker>>(),provider.GetRequiredService<IConfiguration>());
await worker.Tick(default);
Guid eventId;
using(var scope=provider.CreateScope()){
 var db=scope.ServiceProvider.GetRequiredService<HrDbContext>();var row=await db.AutomationEvents.SingleAsync();eventId=row.Id;
 if(row.Status!="Retrying"||row.Attempts!=1||row.LastHttpStatus!=503)throw new Exception("Retry not recorded");
 row.NextAttemptUtc=DateTime.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();
}
fake.Status=HttpStatusCode.NoContent;await worker.Tick(default);
using(var scope=provider.CreateScope()){
 var db=scope.ServiceProvider.GetRequiredService<HrDbContext>();var row=await db.AutomationEvents.SingleAsync();
 if(row.Status!="Delivered"||row.Attempts!=2||row.LeaseUntilUtc!=null||fake.Ids.Distinct().Count()!=1||fake.Ids[0]!=eventId.ToString())throw new Exception("Delivery or stable event ID failed");
 var payload=JsonDocument.Parse(fake.Body!);if(payload.RootElement.GetProperty("data").GetProperty("id").GetInt32()<=0)throw new Exception("Temporary source ID");
 row.Status="Pending";row.Attempts=7;row.NextAttemptUtc=DateTime.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();
}
fake.Status=HttpStatusCode.ServiceUnavailable;await worker.Tick(default);
using(var scope=provider.CreateScope()){
 var db=scope.ServiceProvider.GetRequiredService<HrDbContext>();var row=await db.AutomationEvents.SingleAsync();if(row.Status!="Failed")throw new Exception("Retry limit ignored");
 row.Status="Pending";row.Generation=Guid.NewGuid();row.NextAttemptUtc=DateTime.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();
}
var calls=fake.Ids.Count;await worker.Tick(default);
using(var scope=provider.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<HrDbContext>();if((await db.AutomationEvents.SingleAsync()).Status!="Cancelled"||fake.Ids.Count!=calls)throw new Exception("Stale destination sent");}

foreach(var ip in new[]{"127.0.0.1","10.1.1.1","169.254.1.1","192.168.100.1","172.16.0.1","100.64.0.1","::1","fc00::1","fe80::1","::ffff:127.0.0.1"})if(AutomationSecurity.PublicAddress(IPAddress.Parse(ip)))throw new Exception("Private address allowed");
if(!AutomationSecurity.PublicAddress(IPAddress.Parse("8.8.8.8")))throw new Exception("Public address rejected");
using(var handler=(SocketsHttpHandler)AutomationSecurity.Handler())if(handler.AllowAutoRedirect||handler.UseProxy)throw new Exception("Unsafe outbound handler");
Console.WriteLine("PASS atomic rollback, generated IDs, retry/acknowledgment, eight-attempt limit, stale destination cancellation, stable event IDs and private-network/redirect guards. No external request was sent.");
class FakeClient:HttpMessageHandler,IHttpClientFactory{
 public HttpStatusCode Status=HttpStatusCode.ServiceUnavailable;public List<string> Ids=[];public string? Body;
 public HttpClient CreateClient(string name)=>new(this,false);
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Ids.Add(request.Headers.GetValues("X-Garowe-Event-Id").Single());if(request.Headers.GetValues("X-Garowe-Webhook-Key").Single()!="fixture-password-never-log")throw new Exception("Header authentication missing");Body=await request.Content!.ReadAsStringAsync(ct);return new(Status);}
}
