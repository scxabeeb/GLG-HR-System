using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Hr;
[Authorize(Roles = "HR,Admin")]
public class EmployeesModel(HrDbContext db) : PageModel
{
    public List<Employee> Items = [];
    public async Task OnGetAsync()
    {
        Items = await db.Employees.OrderBy(x => x.FullName).ToListAsync();
    }
}
