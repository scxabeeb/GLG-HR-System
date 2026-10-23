using System.Text.Json;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles="Admin")]
public class PasswordResetsModel(IWebHostEnvironment env):PageModel
{
 public List<LocalPasswordResetMessage> Items=[];
 public async Task<IActionResult> OnGetAsync(){if(!env.IsDevelopment())return NotFound();Response.Headers.CacheControl="no-store";var folder=Path.Combine(env.ContentRootPath,"App_Data","password-reset-outbox");if(Directory.Exists(folder))foreach(var file in Directory.EnumerateFiles(folder,"*.json").OrderDescending().Take(100)){try{var item=JsonSerializer.Deserialize<LocalPasswordResetMessage>(await System.IO.File.ReadAllTextAsync(file));if(item!=null&&item.CreatedUtc>DateTime.UtcNow.AddHours(-1)&&item.CreatedUtc<=DateTime.UtcNow)Items.Add(item);}catch(JsonException){}}Items=Items.OrderByDescending(x=>x.CreatedUtc).ToList();return Page();}
}
