using System.ComponentModel.DataAnnotations;

namespace HrSystemDhg.Web.Models;

public class MeetingAgendaItem
{
    public int Id { get; set; }
    public int MeetingId { get; set; }
    [MaxLength(255)] public string AuthorId { get; set; } = "";
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(2000)] public string? Notes { get; set; }
}
