using HrSystemDhg.Web.Services;
using Xunit;

public class OperationsPolicyTests
{
    [Theory]
    [InlineData("https://meet.google.com/abc-defg-hij", true)]
    [InlineData("https://teams.microsoft.com/l/meetup-join/fixture", true)]
    [InlineData("https://teams.live.com/meet/123", true)]
    [InlineData("https://teams.cloud.microsoft/meet/123?p=abc", true)]
    [InlineData("https://meet.google.com.evil.example/abc-defg-hij", false)]
    [InlineData("https://evil.example@meet.google.com/abc-defg-hij", false)]
    [InlineData("https://meet.google.com:8443/abc-defg-hij", false)]
    [InlineData("http://teams.microsoft.com/l/meetup-join/fixture", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("https://meet.google.com/new", false)]
    public void JoinLinksUseSupportedVideoHosts(string url, bool expected) =>
        Assert.Equal(expected, OperationsPolicy.IsJoinUrl(url));

    [Fact]
    public void MeetingTimeRoundTripsMogadishuToUtc()
    {
        var local = new DateTime(2026, 11, 9, 14, 30, 0);
        var utc = OperationsPolicy.ToUtc(local);
        Assert.Equal(new DateTime(2026, 11, 9, 11, 30, 0, DateTimeKind.Utc), utc);
        Assert.Equal(local, OperationsPolicy.Local(utc));
    }
}
