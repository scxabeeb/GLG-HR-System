using System.Net;
using System.Net.Mail;
using System.Text.Json;
namespace HrSystemDhg.Web.Services;
public interface IPasswordResetDelivery { Task SendAsync(string email,string link); }
public record LocalPasswordResetMessage(string Email,string Link,DateTime CreatedUtc);
public class PasswordResetDelivery(IConfiguration config,IWebHostEnvironment env):IPasswordResetDelivery
{
 public string Outbox=>Path.Combine(env.ContentRootPath,"App_Data","password-reset-outbox");
 public async Task SendAsync(string email,string link)
 {
  if(env.IsDevelopment()&&string.IsNullOrWhiteSpace(config["Smtp:Host"]))
  {
   Directory.CreateDirectory(Outbox);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(Outbox,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
   var path=Path.Combine(Outbox,Guid.NewGuid().ToString("N")+".json");
   var options=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write};
   if(!OperatingSystem.IsWindows())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
   await using var stream=new FileStream(path,options);
   await JsonSerializer.SerializeAsync(stream,new LocalPasswordResetMessage(email,link,DateTime.UtcNow));return;
  }
  var host=config["Smtp:Host"]??throw new InvalidOperationException("Password reset delivery is not configured.");
  using var smtp=new SmtpClient(host,config.GetValue<int?>("Smtp:Port")??587){EnableSsl=true};
  if(!string.IsNullOrWhiteSpace(config["Smtp:Username"]))smtp.Credentials=new NetworkCredential(config["Smtp:Username"],config["Smtp:Password"]);
  using var message=new MailMessage(config["Smtp:From"]??throw new InvalidOperationException("SMTP sender is not configured."),email,"Reset your Garowe HR password",$"A password reset was requested for your Garowe HR account.\n\nOpen this link within one hour:\n{link}\n\nIf you did not request this, ignore this message.");
  await smtp.SendMailAsync(message);
 }
}
