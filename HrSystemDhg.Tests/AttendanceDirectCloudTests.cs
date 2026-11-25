using System.Net;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Http;
using Xunit;
public class AttendanceDirectCloudTests
{
    [Theory]
    [InlineData("192.168.1.10")][InlineData("10.0.0.1")][InlineData("172.16.1.1")]
    [InlineData("127.0.0.1")][InlineData("100.64.0.1")][InlineData("169.254.1.1")]
    [InlineData("0.0.0.0")][InlineData("224.0.0.1")][InlineData("203.0.113.1")]
    [InlineData("8.8.8.8,1.1.1.1")]
    public void PrivateReservedAndAmbiguousAddressesCannotAuthorizeDirectCloud(string value)=>Assert.Null(AttendanceDirectCloud.PublicAddress(value));
    [Theory]
    [InlineData("8.8.8.8")][InlineData("192.2.1.1")][InlineData("192.0.3.1")]
    public void PublicIpv4AddressesCanBeExplicitlyAllowlisted(string address)=>Assert.Equal(address,AttendanceDirectCloud.PublicAddress(address));
    [Fact]
    public void DirectConnectionRequiresHttpsAndIgnoresSpoofedForwardedHeaders()
    {
        var context=new DefaultHttpContext();context.Connection.RemoteIpAddress=IPAddress.Parse("8.8.8.8");
        context.Request.Headers["X-Forwarded-Proto"]="https";context.Request.Headers["X-Real-IP"]="1.1.1.1";
        Assert.Null(AttendanceDirectCloud.Source(context,false));
        context.Request.Scheme="https";Assert.Equal("8.8.8.8",AttendanceDirectCloud.Source(context,false));
    }
    [Fact]
    public void RailwayRequiresInternalPeerHttpsAndEdgeMetadata()
    {
        var context=new DefaultHttpContext();context.Connection.RemoteIpAddress=IPAddress.Parse("100.64.1.2");
        context.Request.Headers["X-Forwarded-Proto"]="https";context.Request.Headers["X-Real-IP"]="8.8.8.8";
        Assert.Null(AttendanceDirectCloud.Source(context,true));
        context.Request.Headers["X-Railway-Edge"]="test-edge";Assert.Equal("8.8.8.8",AttendanceDirectCloud.Source(context,true));
        context.Connection.RemoteIpAddress=IPAddress.Parse("1.1.1.1");Assert.Null(AttendanceDirectCloud.Source(context,true));
        context.Connection.RemoteIpAddress=IPAddress.Parse("100.64.1.2");context.Request.Headers["X-Forwarded-Proto"]="http";Assert.Null(AttendanceDirectCloud.Source(context,true));
    }
}
