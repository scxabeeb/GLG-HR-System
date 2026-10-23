using HrSystemDhg.Web.Data;
using Microsoft.AspNetCore.Authorization;

namespace HrSystemDhg.Web.Pages.Hr;
[Authorize(Roles = "HR,Admin")]
public class DashboardModel(HrDbContext db) : HrSystemDhg.Web.Pages.IndexModel(db)
{
}
