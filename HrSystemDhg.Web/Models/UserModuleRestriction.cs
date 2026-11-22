using System.ComponentModel.DataAnnotations;
namespace HrSystemDhg.Web.Models;

public class UserModuleRestriction
{
    [MaxLength(255)] public string UserId { get; set; } = "";
    [MaxLength(32)] public string Module { get; set; } = "";
}
