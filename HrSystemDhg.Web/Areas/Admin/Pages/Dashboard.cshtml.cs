using HrSystemDhg.Web.Data;
using Microsoft.AspNetCore.Authorization;

namespace HrSystemDhg.Web.Pages.Admin;
[Authorize(Roles = "Admin")]
public class DashboardModel(HrDbContext db) : HrSystemDhg.Web.Pages.IndexModel(db)
{
}
