namespace HrSystemDhg.Web.Services;
public static class ReportDefinitions
{
    public static readonly Dictionary<string, string[]> Sources = new()
    {
        ["Employees"] = ["Number", "Name", "Email", "Job title", "Branch", "Department", "Sub-department", "Hire date", "Termination date", "Status"],
        ["Hires"] = ["Number", "Name", "Email", "Job title", "Branch", "Department", "Sub-department", "Hire date", "Termination date", "Status"],
        ["Attendance"] = ["Employee", "Date", "Time in (EAT)", "Time out (EAT)", "Hours", "Late minutes", "Missing minutes", "Status"],
        ["Salary"] = ["Employee number", "Employee", "Effective from", "Effective to", "Status", "Currency", "Basic", "Housing", "Transport", "Other allowances", "Total"],
        ["Leave"] = ["Employee", "From", "To", "Status", "Reason"],
        ["Balances"] = ["Employee", "Leave type", "Year", "Remaining days"],
        ["Payroll"] = ["Employee", "Currency", "Basic", "Allowances", "Overtime", "Gross", "Attendance", "Unpaid leave", "Net"],
        ["Deductions"] = ["Employee", "Date", "Type", "Calculated", "Applied", "Waived", "Reason"],
        ["Bank"] = ["Employee", "Bank", "Account name", "Account number", "Currency", "Net"]
    };
    public static int[] ColumnIndexes(string columns) => columns.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => int.TryParse(c, out var index) ? index : -1).Distinct().ToArray();
}
