using HrSystemDhg.Web.Services;
using Microsoft.EntityFrameworkCore;
namespace HrSystemDhg.Web.Data;
public partial class HrDbContext
{
    // The domain save and outbox save share one transaction. No network request happens while saving staff data.
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken=default)
    {
        ChangeTracker.DetectChanges();
        var pending=ChangeTracker.Entries().Where(e=>e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && AutomationCapture.Module(e.Entity)!=null)
            .Select(e=>new{Entry=e,State=e.State,Changes=e.Properties.Where(p=>p.IsModified).Select(p=>p.Metadata.Name).ToArray()}).ToArray();
        if(pending.Length==0)return await base.SaveChangesAsync(cancellationToken);
        var config=await AutomationSettings.AsNoTracking().SingleOrDefaultAsync(s=>s.Id==1,cancellationToken);
        if(config?.Enabled!=true)return await base.SaveChangesAsync(cancellationToken);
        var selected=pending.Where(p=>AutomationCapture.Allowed(config,AutomationCapture.Module(p.Entry.Entity)!)).ToArray();
        if(selected.Length==0)return await base.SaveChangesAsync(cancellationToken);
        await using var transaction=Database.CurrentTransaction==null?await Database.BeginTransactionAsync(cancellationToken):null;
        var count=await base.SaveChangesAsync(cancellationToken);
        AutomationEvents.AddRange(selected.Select(p=>AutomationCapture.Capture(p.Entry,config,ActorId,p.State,p.Changes)));
        await base.SaveChangesAsync(cancellationToken);
        if(transaction!=null)await transaction.CommitAsync(cancellationToken);
        return count;
    }
}
