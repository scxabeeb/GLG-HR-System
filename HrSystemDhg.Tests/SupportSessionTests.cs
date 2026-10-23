using System.Security.Claims;
using HrSystemDhg.Web.Services;
using Xunit;

namespace HrSystemDhg.Tests;
public class SupportSessionTests
{
    [Fact]
    public void NormalAccountsHaveNoSupportExpiry() => Assert.False(SupportSession.Expired(new ClaimsPrincipal(), DateTimeOffset.UtcNow));
    [Fact]
    public void MissingSupportExpiryFailsClosed()
    {
        var p = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("support-admin", "admin") }));
        Assert.True(SupportSession.Expired(p, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    public void SupportExpiryIsIndependentOfCookieRenewal(int seconds, bool expired)
    {
        var now = DateTimeOffset.UtcNow;
        var p = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("support-admin", "admin"), new Claim("support-expires", now.AddSeconds(seconds).ToUnixTimeSeconds().ToString()) }));
        Assert.Equal(expired, SupportSession.Expired(p, now));
    }
}
