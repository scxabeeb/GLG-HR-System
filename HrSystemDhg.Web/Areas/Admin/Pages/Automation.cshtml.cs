using System.Security.Cryptography;
using System.Text.Json;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles="Admin")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class AutomationModel(HrDbContext db,IDataProtectionProvider protection):PageModel
{
    [BindProperty] public AutomationSettings Input {get;set;}=new();
    [BindProperty] public string? WebhookPassword {get;set;}
    public bool HasWebhookPassword {get;private set;}
    public bool HasCallbackKey {get;private set;}
    public List<DeliveryRow> Deliveries {get;private set;}=[];
    public record DeliveryRow(Guid Id,string Module,string EventType,string Status,int Attempts,DateTime CreatedUtc,DateTime? DeliveredUtc,int? LastHttpStatus,string? LastError);
    public async Task OnGetAsync(){Input=await db.AutomationSettings.AsNoTracking().SingleOrDefaultAsync(s=>s.Id==1)??new();HasWebhookPassword=!string.IsNullOrEmpty(Input.ProtectedWebhookSecret);HasCallbackKey=Input.CallbackKeyHash!=null;Deliveries=await db.AutomationEvents.AsNoTracking().OrderByDescending(e=>e.CreatedUtc).Take(100).Select(e=>new DeliveryRow(e.Id,e.Module,e.EventType,e.Status,e.Attempts,e.CreatedUtc,e.DeliveredUtc,e.LastHttpStatus,e.LastError)).ToListAsync();}
    public async Task<IActionResult> OnPostAsync(){
        Input.WebhookUrl=(Input.WebhookUrl??"").Trim();
        var current=await db.AutomationSettings.SingleOrDefaultAsync(s=>s.Id==1)??new();
        if(Input.WebhookUrl.Length>0&&!AutomationSecurity.ValidUrl(Input.WebhookUrl))ModelState.AddModelError("","Use a public HTTPS production webhook on port 443, without URL credentials, query or fragment.");
        if(WebhookPassword?.Length>512||!string.IsNullOrEmpty(WebhookPassword)&&WebhookPassword.Length<16)ModelState.AddModelError("","Use a webhook password of 16–512 characters.");
        if(Input.Enabled&&(string.IsNullOrWhiteSpace(Input.WebhookUrl)||string.IsNullOrEmpty(WebhookPassword)&&string.IsNullOrEmpty(current.ProtectedWebhookSecret)))ModelState.AddModelError("","Configure the webhook URL and shared password before enabling delivery.");
        if(!ModelState.IsValid){WebhookPassword=null;await OnGetAsync();return Page();}
        if(current.WebhookUrl!=Input.WebhookUrl.Trim())current.Generation=Guid.NewGuid();
        current.WebhookUrl=Input.WebhookUrl.Trim();current.Enabled=Input.Enabled;current.Tasks=Input.Tasks;current.Meetings=Input.Meetings;current.Attendance=Input.Attendance;current.Payroll=Input.Payroll;current.AllowReminders=Input.AllowReminders;current.IncludeContacts=Input.IncludeContacts;current.IncludePayrollAmounts=Input.IncludePayrollAmounts;
        if(!string.IsNullOrEmpty(WebhookPassword))current.ProtectedWebhookSecret=protection.CreateProtector("Garowe.Automation.Webhook.v1").Protect(WebhookPassword);
        if(!await db.AutomationSettings.AnyAsync(s=>s.Id==1))db.AutomationSettings.Add(current);
        db.AuditLogs.Add(new(){Actor=User.Identity!.Name!,Action="Updated n8n automation settings; enabled="+current.Enabled});await db.SaveChangesAsync();TempData["AutomationNotice"]="Automation settings saved.";return RedirectToPage();
    }
    public async Task<IActionResult> OnPostRotateAsync(){var current=await db.AutomationSettings.SingleOrDefaultAsync(s=>s.Id==1);if(current==null){current=new();db.AutomationSettings.Add(current);}var key=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));current.CallbackKeyHash=AutomationSecurity.Hash(key);db.AuditLogs.Add(new(){Actor=User.Identity!.Name!,Action="Rotated read-only n8n API key"});await db.SaveChangesAsync();return File(JsonSerializer.SerializeToUtf8Bytes(new{header="X-Garowe-Automation-Key",key,summaryUrl="https://erp.garowecity.pl.so/api/integrations/n8n/summary?module=tasks",timeZone="Africa/Mogadishu"}),"application/json","garowe-n8n-read-only-key.json");}
    public async Task<IActionResult> OnPostReminderKeyAsync(){var current=await db.AutomationSettings.SingleOrDefaultAsync(s=>s.Id==1);if(current==null){current=new();db.AutomationSettings.Add(current);}var key=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));current.ReminderKeyHash=AutomationSecurity.Hash(key);db.AuditLogs.Add(new(){Actor=User.Identity!.Name!,Action="Rotated limited n8n reminder key"});await db.SaveChangesAsync();return File(JsonSerializer.SerializeToUtf8Bytes(new{header="X-Garowe-Reminder-Key",key,url="https://erp.garowecity.pl.so/api/integrations/n8n/reminders",scope="Queue preset in-app reminders and admin digests only"}),"application/json","garowe-n8n-reminder-key.json");}
    public async Task<IActionResult> OnPostTestAsync(){var config=await db.AutomationSettings.AsNoTracking().SingleOrDefaultAsync(s=>s.Id==1);if(config==null||!AutomationSecurity.ValidUrl(config.WebhookUrl)||string.IsNullOrEmpty(config.ProtectedWebhookSecret)){TempData["AutomationNotice"]="Save the webhook URL and shared password first.";return RedirectToPage();}var item=new AutomationEvent{Module="system",EventType="integration.test",Generation=config.Generation};item.Payload=JsonSerializer.Serialize(new{schemaVersion=1,eventId=item.Id,eventType=item.EventType,module=item.Module,timeZone="Africa/Mogadishu",occurredUtc=item.CreatedUtc,data=new{message="Garowe City connection test. No employee data."}});db.AutomationEvents.Add(item);await db.SaveChangesAsync();TempData["AutomationNotice"]="Test queued. Refresh delivery history for the HTTP response; this does not verify a WhatsApp message.";return RedirectToPage();}
    public async Task<IActionResult> OnPostRetryAsync(Guid id){var item=await db.AutomationEvents.SingleOrDefaultAsync(e=>e.Id==id);var config=await db.AutomationSettings.AsNoTracking().SingleOrDefaultAsync(s=>s.Id==1);if(item==null||config==null||item.Generation!=config.Generation||item.Status!="Failed")return BadRequest();item.Status="Pending";item.Attempts=0;item.NextAttemptUtc=DateTime.UtcNow;item.LeaseUntilUtc=null;item.LastError=null;await db.SaveChangesAsync();return RedirectToPage();}
    public async Task<IActionResult> OnPostCancelAsync(Guid id){var item=await db.AutomationEvents.SingleOrDefaultAsync(e=>e.Id==id);if(item==null||item.Status=="Delivered")return BadRequest();item.Status="Cancelled";item.LeaseUntilUtc=null;await db.SaveChangesAsync();return RedirectToPage();}
    public IActionResult OnGetWorkflow(string kind="events")=>File(AutomationWorkflows.Build(kind),"application/json","garowe-n8n-"+kind+".json");
}
