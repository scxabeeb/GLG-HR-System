using System.Text;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
namespace HrSystemDhg.Web.Services;
public class PasswordRecoveryService(UserManager<ApplicationUser> users,HrDbContext db,IPasswordResetDelivery delivery,IConfiguration config,IWebHostEnvironment env,ILogger<PasswordRecoveryService> logger)
{
 public static string BuildLink(string origin,string email,string token,bool development)
 {
  if(!Uri.TryCreate(origin,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")||(!development&&uri.Scheme!="https")||!string.IsNullOrEmpty(uri.UserInfo)||uri.AbsolutePath!="/"||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment))throw new InvalidOperationException("Configure a valid PasswordReset:PublicOrigin (HTTPS outside Development).");
  return QueryHelpers.AddQueryString(uri.GetLeftPart(UriPartial.Authority)+"/Identity/Account/ResetPassword",new Dictionary<string,string?>{{"email",email},{"code",WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))}});
 }
 public async Task RequestAsync(string email)
 {
  var user=await users.FindByEmailAsync(email.Trim());if(user==null||!user.IsEnabled||string.IsNullOrWhiteSpace(user.Email))return;
  var token=await users.GeneratePasswordResetTokenAsync(user);
  var origin=config["PasswordReset:PublicOrigin"]??(env.IsDevelopment()?"http://127.0.0.1:5189":throw new InvalidOperationException("Configure PasswordReset:PublicOrigin."));
  var link=BuildLink(origin,user.Email,token,env.IsDevelopment());
  try{await delivery.SendAsync(user.Email,link);}catch(Exception e)when(e is System.Net.Mail.SmtpException or InvalidOperationException or IOException){logger.LogWarning("Password-reset delivery unavailable ({FailureType}).",e.GetType().Name);}
 }
 public async Task<IdentityResult> ResetAsync(string email,string code,string password)
 {
  string token;try{token=Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));}catch(FormatException){return Invalid();}
  var user=await users.FindByEmailAsync(email.Trim());if(user==null||!user.IsEnabled)return Invalid();
  await using var tx=await db.Database.BeginTransactionAsync();var result=await users.ResetPasswordAsync(user,token,password);if(!result.Succeeded)return result;
  user.MustChangePassword=false;user.AccessFailedCount=0;user.LockoutEnd=null;result=await users.UpdateAsync(user);if(!result.Succeeded)return result;
  db.AuditLogs.Add(new(){Actor=user.Id,Action="Account password reset using a verified recovery token"});await db.SaveChangesAsync();await tx.CommitAsync();return IdentityResult.Success;
 }
 private static IdentityResult Invalid()=>IdentityResult.Failed(new IdentityError{Code="InvalidToken",Description="This reset link is invalid or expired. Request another link."});
}
