using System.Text;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;
namespace HrSystemDhg.Tests;
public class PasswordRecoveryTests
{
 [Fact] public void ResetLinkPreservesEmailAndToken(){var token="a+token/with=padding";var url=PasswordRecoveryService.BuildLink("https://hr.example.com","person+hr@example.com",token,false);var uri=new Uri(url);var query=QueryHelpers.ParseQuery(uri.Query);Assert.Equal("hr.example.com",uri.Host);Assert.Equal("person+hr@example.com",query["email"].ToString());Assert.Equal(token,Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(query["code"].ToString())));}
 [Theory][InlineData("http://hr.example.com")][InlineData("https://user:password@hr.example.com")][InlineData("https://hr.example.com/untrusted")][InlineData("https://hr.example.com?redirect=elsewhere")][InlineData("javascript:alert(1)")]
 public void UnsafeProductionOriginsAreRejected(string origin)=>Assert.Throws<InvalidOperationException>(()=>PasswordRecoveryService.BuildLink(origin,"a@example.com","token",false));
 [Fact] public void LocalDevelopmentSupportsLoopbackHttp()=>Assert.StartsWith("http://127.0.0.1:5189/Identity/Account/ResetPassword?",PasswordRecoveryService.BuildLink("http://127.0.0.1:5189","a@example.com","token",true));
}
