using HrSystemDhg.Web.Data;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Pages.Hr;
[Authorize(Roles = "HR,Admin")]
public class HistoryEditModel(HrDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Kind { get; set; } = "Education";

    [BindProperty]
    public string Organization { get; set; } = "";

    [BindProperty]
    public string Title { get; set; } = "";

    [BindProperty]
    public string Field { get; set; } = "";

    [BindProperty]
    public int? StartYear { get; set; }

    [BindProperty]
    public int? EndYear { get; set; }

    [BindProperty]
    public string Grade { get; set; } = "";

    [BindProperty]
    public string Country { get; set; } = "";

    [BindProperty]
    public DateTime? From { get; set; }

    [BindProperty]
    public DateTime? To { get; set; }

    [BindProperty]
    public string Responsibilities { get; set; } = "";

    [BindProperty]
    public string Reference { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        if (Kind == "Education")
        {
            var e = await db.Educations.SingleOrDefaultAsync(x => x.Id == Id);
            if (e == null)
                return NotFound();
            Organization = e.Institution;
            Title = e.Qualification;
            Field = e.FieldOfStudy;
            StartYear = e.StartYear;
            EndYear = e.EndYear;
            Grade = e.Grade;
        }
        else if (Kind == "Experience")
        {
            var e = await db.WorkExperiences.SingleOrDefaultAsync(x => x.Id == Id);
            if (e == null)
                return NotFound();
            Organization = e.Employer;
            Title = e.JobTitle;
            Country = e.Country;
            From = e.From;
            To = e.To;
            Responsibilities = e.Responsibilities;
            Reference = e.Reference;
        }
        else if (Kind == "Document")
        {
            var e = await db.EmployeeDocuments.SingleOrDefaultAsync(x => x.Id == Id);
            if (e == null)
                return NotFound();
            Title = e.Type;
            To = e.ExpiryDate;
        }
        else
            return BadRequest();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Title) || (Kind != "Document" && string.IsNullOrWhiteSpace(Organization)) || EndYear < StartYear || To < From)
        {
            ModelState.AddModelError("", "Enter valid details and date ranges.");
            return Page();
        }

        int employeeId;
        if (Kind == "Education")
        {
            var e = await db.Educations.SingleAsync(x => x.Id == Id);
            employeeId = e.EmployeeId;
            e.Institution = Organization;
            e.Qualification = Title;
            e.FieldOfStudy = Field;
            e.StartYear = StartYear;
            e.EndYear = EndYear;
            e.Grade = Grade;
        }
        else if (Kind == "Experience")
        {
            if (From == null)
                return BadRequest();
            var e = await db.WorkExperiences.SingleAsync(x => x.Id == Id);
            employeeId = e.EmployeeId;
            e.Employer = Organization;
            e.JobTitle = Title;
            e.Country = Country;
            e.From = From.Value;
            e.To = To;
            e.Responsibilities = Responsibilities;
            e.Reference = Reference;
        }
        else if (Kind == "Document")
        {
            var e = await db.EmployeeDocuments.SingleAsync(x => x.Id == Id);
            employeeId = e.EmployeeId;
            e.Type = Title;
            e.ExpiryDate = To;
        }
        else
            return BadRequest();
        db.AuditLogs.Add(new() { Actor = User.Identity!.Name!, Action = $"Edited {Kind} {Id} for employee {employeeId}" });
        await db.SaveChangesAsync();
        return RedirectToPage("Onboarding", new { employeeId, step = Kind == "Education" ? 3 : Kind == "Experience" ? 4 : 6 });
    }
}
