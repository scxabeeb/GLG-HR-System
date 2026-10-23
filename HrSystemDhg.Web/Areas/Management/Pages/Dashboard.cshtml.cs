using HrSystemDhg.Web.Data;
using Microsoft.AspNetCore.Authorization;

namespace HrSystemDhg.Web.Pages.Management;
[Authorize(Roles = "Management,Admin")]
public class DashboardModel(HrDbContext db) : HrSystemDhg.Web.Pages.IndexModel(db)
{
}
