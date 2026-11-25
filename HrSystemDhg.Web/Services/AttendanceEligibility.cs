using HrSystemDhg.Web.Models;
namespace HrSystemDhg.Web.Services;
public static class AttendanceEligibility
{
    public static DateTime RegistrationDate(Employee employee) => employee.RegisteredUtc.HasValue
        ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(employee.RegisteredUtc.Value,DateTimeKind.Utc),TimeZoneInfo.FindSystemTimeZoneById("Africa/Mogadishu")).Date
        : employee.HireDate.Date;
    public static DateTime StartDate(Employee employee)
    {
        var afterRegistration=RegistrationDate(employee).AddDays(1);
        return employee.HireDate.Date > afterRegistration ? employee.HireDate.Date : afterRegistration;
    }
}
