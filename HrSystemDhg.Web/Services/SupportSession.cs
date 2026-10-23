using System.Security.Claims;

namespace HrSystemDhg.Web.Services;
public static class SupportSession
{
    public static bool Expired(ClaimsPrincipal principal, DateTimeOffset now)
    {
        if (!principal.HasClaim(x => x.Type == "support-admin"))
            return false;
        return !long.TryParse(principal.FindFirst("support-expires")?.Value, out var expires) || now.ToUnixTimeSeconds() >= expires;
    }
}
