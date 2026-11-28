using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
namespace HrSystemDhg.Web.Services;
public static class AutomationSecurity
{
    public static bool ValidUrl(string? value)=>Uri.TryCreate(value,UriKind.Absolute,out var url)&&url.Scheme=="https"&&url.Port==443&&url.UserInfo.Length==0&&url.Fragment.Length==0&&url.Query.Length==0&&url.HostNameType!=UriHostNameType.Unknown&&url.Host.Length<=253;
    public static bool PublicAddress(IPAddress ip) {
        if(ip.IsIPv4MappedToIPv6)ip=ip.MapToIPv4();
        if(IPAddress.IsLoopback(ip))return false;
        if(ip.AddressFamily==AddressFamily.InterNetwork){var b=ip.GetAddressBytes();return !(b[0] is 0 or 10 or 127||b[0]>=224||b[0]==169&&b[1]==254||b[0]==172&&b[1]>=16&&b[1]<=31||b[0]==192&&(b[1]==168||b[1]==0&&b[2] is 0 or 2)||b[0]==100&&b[1]>=64&&b[1]<=127||b[0]==198&&b[1] is 18 or 19||b[0]==198&&b[1]==51&&b[2]==100||b[0]==203&&b[1]==0&&b[2]==113);}
        var bytes=ip.GetAddressBytes();return ip.AddressFamily==AddressFamily.InterNetworkV6&&!ip.IsIPv6LinkLocal&&!ip.IsIPv6Multicast&&!ip.IsIPv6SiteLocal&&(bytes[0]&0xe0)==0x20&&!(bytes[0]==0x20&&bytes[1]==0x01&&bytes[2]==0x0d&&bytes[3]==0xb8);
    }
    public static HttpMessageHandler Handler()=>new SocketsHttpHandler {
        AllowAutoRedirect=false,UseCookies=false,UseProxy=false,
        ConnectCallback=async(context,ct)=>{
            var addresses=await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host,ct);
            if(addresses.Length==0||addresses.Any(ip=>!PublicAddress(ip)))throw new HttpRequestException("Webhook requires a public destination.");
            Exception? failure=null;
            foreach(var ip in addresses){var socket=new Socket(ip.AddressFamily,SocketType.Stream,ProtocolType.Tcp);try{await socket.ConnectAsync(new IPEndPoint(ip,443),ct);return new NetworkStream(socket,true);}catch(Exception ex) when(ex is SocketException or OperationCanceledException){socket.Dispose();failure=ex;if(ct.IsCancellationRequested)throw;}}
            throw new HttpRequestException("Webhook connection unavailable.",failure);
        }
    };
    public static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool Matches(string? token,string? hash){if(string.IsNullOrEmpty(token)||token.Length>512||hash?.Length!=64)return false;try{return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Hash(token)),Convert.FromHexString(hash));}catch(FormatException){return false;}}
}
public sealed class AutomationDelivery(IHttpClientFactory clients,IDataProtectionProvider protection)
{
    public async Task<int> SendAsync(AutomationSettings config,AutomationEvent item,CancellationToken ct) {
        if(!AutomationSecurity.ValidUrl(config.WebhookUrl))throw new InvalidOperationException("Configure a valid public HTTPS webhook.");
        var secret=protection.CreateProtector("Garowe.Automation.Webhook.v1").Unprotect(config.ProtectedWebhookSecret!);
        using var request=new HttpRequestMessage(HttpMethod.Post,config.WebhookUrl){Content=new StringContent(item.Payload,Encoding.UTF8,"application/json")};
        request.Headers.Add("X-Garowe-Webhook-Key",secret);
        request.Headers.Add("X-Garowe-Event-Id",item.Id.ToString());
        request.Headers.Add("X-Garowe-Event-Type",item.EventType);
        using var response=await clients.CreateClient("automation").SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        return (int)response.StatusCode;
    }
}
public sealed class AutomationWorker(IServiceScopeFactory scopes,ILogger<AutomationWorker> logger,IConfiguration configuration):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if(configuration.GetValue<bool>("Automation:DisableWorker"))return;
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(10));
        while(!stoppingToken.IsCancellationRequested){try{await Tick(stoppingToken);}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}catch(Exception ex){logger.LogWarning("Automation delivery cycle unavailable ({ErrorType})",ex.GetType().Name);}try{if(!await timer.WaitForNextTickAsync(stoppingToken))break;}catch(OperationCanceledException){break;}}
    }
    public async Task Tick(CancellationToken ct) {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<HrDbContext>();
        var config=await db.AutomationSettings.AsNoTracking().SingleOrDefaultAsync(s=>s.Id==1,ct);if(config==null||string.IsNullOrEmpty(config.ProtectedWebhookSecret))return;
        var now=DateTime.UtcNow;
        var ids=await db.AutomationEvents.Where(e=>(e.Status=="Pending"||e.Status=="Retrying")&&e.NextAttemptUtc<=now&&(e.LeaseUntilUtc==null||e.LeaseUntilUtc<now)&&(config.Enabled||e.Module=="system")).OrderBy(e=>e.CreatedUtc).Select(e=>e.Id).Take(25).ToArrayAsync(ct);
        foreach(var id in ids){
            var claimed=await db.AutomationEvents.Where(e=>e.Id==id&&(e.Status=="Pending"||e.Status=="Retrying")&&(e.LeaseUntilUtc==null||e.LeaseUntilUtc<now)).ExecuteUpdateAsync(s=>s.SetProperty(e=>e.LeaseUntilUtc,now.AddMinutes(2)),ct);if(claimed==0)continue;
            var item=await db.AutomationEvents.SingleAsync(e=>e.Id==id,ct);
            if(item.Generation!=config.Generation||item.Module!="system"&&!AutomationCapture.Allowed(config,item.Module)){item.Status="Cancelled";item.LastError="Destination or module configuration changed.";item.LeaseUntilUtc=null;await db.SaveChangesAsync(ct);continue;}
            item.Attempts++;int? status=null;
            try{status=await scope.ServiceProvider.GetRequiredService<AutomationDelivery>().SendAsync(config,item,ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}catch(Exception ex)when(ex is HttpRequestException or OperationCanceledException or CryptographicException or InvalidOperationException){item.LastError="Connection, timeout or credential error. No response body is stored.";}
            item.LastHttpStatus=status;item.LeaseUntilUtc=null;
            if(status is >=200 and <300){item.Status="Delivered";item.DeliveredUtc=DateTime.UtcNow;item.LastError=null;}
            else{item.Status=item.Attempts>=8?"Failed":"Retrying";item.NextAttemptUtc=DateTime.UtcNow.AddSeconds(Math.Min(3600,15*Math.Pow(2,item.Attempts)));if(status!=null)item.LastError=$"Webhook returned HTTP {status}.";}
            await db.SaveChangesAsync(ct);
        }
        // Retain a bounded history. Failed/pending records remain available for review.
        await db.AutomationEvents.Where(e=>(e.Status=="Delivered"||e.Status=="Cancelled")&&e.CreatedUtc<DateTime.UtcNow.AddDays(-30)).ExecuteDeleteAsync(ct);
    }
}
