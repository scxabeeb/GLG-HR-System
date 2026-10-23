using HrSystemDhg.Web.Services;

var passed = 0;
void Check(bool result, string name)
{
    if (!result)
        throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS " + name);
    passed++;
}

var paid = PayrollPolicy.DayDeductions(100, true, true, true, 120, 1, 1);
Check(paid == (0m, 0m), "Approved paid leave suppresses absence and late deductions");
var unpaid = PayrollPolicy.DayDeductions(100, true, false, true, 120, 1, 1);
Check(unpaid == (0m, 100m), "Unpaid leave deducts once without attendance deduction");
Check(PayrollPolicy.DayDeductions(100, false, false, true, 0, 1, 1) == (100m, 0m), "Unapproved absence deducts daily rate");
Check(PayrollPolicy.DayDeductions(100, false, false, false, 30, 1, .5m) == (15m, 0m), "Late deduction uses configured minute rate");
Check(PayrollPolicy.DayDeductions(100, false, false, false, -10, 1, .5m) == (0m, 0m), "Negative late minutes cannot increase pay");
Check(!PayrollPolicy.IsWorkingDay(new DateTime(2026, 10, 24)), "Saturday excluded by default workweek");
Check(PayrollPolicy.IsWorkingDay(new DateTime(2026, 10, 26)), "Monday included in default workweek");
Console.WriteLine($"{passed} payroll policy checks passed.");
