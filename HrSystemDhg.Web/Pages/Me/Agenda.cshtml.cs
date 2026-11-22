using System.ComponentModel.DataAnnotations;
using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Me;

public class AgendaModel(HrDbContext db) : PageModel
{
    [BindProperty] public Entry Input { get; set; } = new();
    public List<AgendaItem> Items { get; private set; } = [];

    public class Entry
    {
        public int Id { get; set; }
        [Required, StringLength(160)] public string Title { get; set; } = "";
        [StringLength(3000)] public string? Notes { get; set; }
        [DataType(DataType.Date)] public DateTime? Date { get; set; }
    }

    private IQueryable<AgendaItem> Own => db.AgendaItems.Where(item => item.OwnerId == db.ActorId);

    private async Task LoadAsync() => Items = await Own.AsNoTracking()
        .OrderBy(item => item.Completed).ThenBy(item => item.Date == null)
        .ThenBy(item => item.Date).ThenByDescending(item => item.Id).ToListAsync();

    public async Task<IActionResult> OnGetAsync(int? edit)
    {
        if (edit.HasValue)
        {
            var item = await Own.AsNoTracking().SingleOrDefaultAsync(x => x.Id == edit);
            if (item == null) return NotFound();
            Input = new Entry { Id = item.Id, Title = item.Title, Notes = item.Notes, Date = item.Date };
        }
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }
        var item = Input.Id == 0 ? new AgendaItem { OwnerId = db.ActorId! } :
            await Own.SingleOrDefaultAsync(x => x.Id == Input.Id);
        if (item == null) return NotFound();
        item.Title = Input.Title.Trim();
        item.Notes = Input.Notes?.Trim();
        item.Date = Input.Date?.Date;
        if (Input.Id == 0) db.AgendaItems.Add(item);
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, bool completed)
    {
        if (await Own.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Completed, completed)) == 0)
            return NotFound();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (await Own.Where(x => x.Id == id).ExecuteDeleteAsync() == 0)
            return NotFound();
        return RedirectToPage();
    }
}
