using System.Net;
namespace HrSystemDhg.Web.Services;

// Direct ADMS has no bearer-header support. Administrators must explicitly opt a device
// into an HTTPS + serial + office public IPv4 allowlist. Connector authentication is separate.
public static class AttendanceDirectCloud
{
    public static string? PublicAddress(string? value)
    {
        if (!IPAddress.TryParse(value,out var address)) return null;
        if (address.IsIPv4MappedToIPv6) address=address.MapToIPv4();
        var b=address.GetAddressBytes();
        if (b.Length!=4 || b[0] is 0 or 10 or 127 || b[0]>=224 ||
            b[0]==100 && b[1] is >=64 and <=127 || b[0]==169 && b[1]==254 ||
            b[0]==172 && b[1] is >=16 and <=31 || b[0]==192 && (b[1]==168 || b[1]==0 && b[2] is 0 or 2 || b[1]==88 && b[2]==99) ||
            b[0]==198 && (b[1] is 18 or 19 || b[1]==51 && b[2]==100) ||
            b[0]==203 && b[1]==0 && b[2]==113) return null;
        return address.ToString();
    }
    public static string? Source(HttpContext context,bool railwayEdge)
    {
        if (!railwayEdge) return context.Request.IsHttps ? PublicAddress(context.Connection.RemoteIpAddress?.ToString()) : null;
        var peer=context.Connection.RemoteIpAddress;
        if(peer?.IsIPv4MappedToIPv6==true)peer=peer.MapToIPv4();
        var b=peer?.GetAddressBytes();
        var internalPeer=b is {Length:4} && (b[0]==10 || b[0]==100 && b[1] is >=64 and <=127 || b[0]==172 && b[1] is >=16 and <=31 || b[0]==192 && b[1]==168)
            || b is {Length:16} && (b[0]&0xfe)==0xfc;
        // Railway's HTTP edge replaces these headers; trust them only in Railway and
        // from its private peer. Never accept caller-supplied forwarding headers directly.
        if(!internalPeer || string.IsNullOrWhiteSpace(context.Request.Headers["X-Railway-Edge"]) ||
            context.Request.Headers["X-Forwarded-Proto"]!="https")return null;
        return PublicAddress(context.Request.Headers["X-Real-IP"].ToString());
    }
}
