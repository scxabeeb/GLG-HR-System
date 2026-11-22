using System.ComponentModel.DataAnnotations;

namespace HrSystemDhg.Web.Models;

public class AgendaItem
{
    public int Id { get; set; }
    [MaxLength(255)] public string OwnerId { get; set; } = "";
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [StringLength(3000)] public string? Notes { get; set; }
    public DateTime? Date { get; set; }
    public bool Completed { get; set; }
}
