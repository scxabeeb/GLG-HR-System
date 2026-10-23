using Microsoft.AspNetCore.Identity;

namespace HrSystemDhg.Web.Models;
public partial class ApplicationUser : IdentityUser
{
    public int? EmployeeId { get; set; }
    public bool MustChangePassword { get; set; }
}

public partial class Branch
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string City { get; set; } = "";
    public string Timezone { get; set; } = "Africa/Mogadishu";
    public bool IsActive { get; set; } = true;
}

public partial class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public int BranchId { get; set; }
    public bool IsActive { get; set; } = true;
}

public partial class SubDepartment
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
}

public partial class Employee
{
    public int Id { get; set; }
    public string EmployeeNumber { get; set; } = "";
    public string FullName { get; set; } = "";
    public string WorkEmail { get; set; } = "";
    public string? NationalId { get; set; }
    public string? ZkPin { get; set; }
    public string Phone { get; set; } = "";
    public DateTime HireDate { get; set; } = HrSystemDhg.Web.Services.BusinessClock.Today;
    public DateTime? TerminationDate { get; set; }
    public string Status { get; set; } = "Active";
}

public partial class EmployeeAssignment
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int BranchId { get; set; }
    public int DepartmentId { get; set; }
    public int? SubDepartmentId { get; set; }
    public string JobTitle { get; set; } = "";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public partial class Education
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string Institution { get; set; } = "";
    public string Qualification { get; set; } = "";
    public string FieldOfStudy { get; set; } = "";
    public int? EndYear { get; set; }
}

public partial class WorkExperience
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string Employer { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public DateTime From { get; set; }
    public DateTime? To { get; set; }
}

public partial class EmployeeDocument
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string Type { get; set; } = "";
    public string StoragePath { get; set; } = "";
    public DateTime? ExpiryDate { get; set; }
}

public partial class SalaryVersion
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public decimal Basic { get; set; }
    public decimal Allowances { get; set; }
    public string Currency { get; set; } = "USD";
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public bool IsDraft { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public partial class WorkRule
{
    public int Id { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public decimal DailyHours { get; set; } = 8;
    public int StartHour { get; set; } = 8;
    public int GraceMinutes { get; set; } = 10;
    public int BreakMinutes { get; set; } = 60;
}

public partial class DeductionRule
{
    public int Id { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public decimal AbsenceMultiplier { get; set; } = 1;
    public decimal LatePerMinute { get; set; } = 0.1m;
}

public partial class ZkDevice
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public DateTime? LastSuccess { get; set; }
    public string? LastError { get; set; }
}

public partial class ZkPunch
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string Pin { get; set; } = "";
    public DateTime PunchTime { get; set; }
}

public partial class AttendanceDay
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime Date { get; set; }
    public DateTime? FirstIn { get; set; }
    public DateTime? LastOut { get; set; }
    public decimal WorkedHours { get; set; }
    public int LateMinutes { get; set; }
    public string Status { get; set; } = "Absent";
}

public partial class LeaveType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsPaid { get; set; } = true;
    public decimal AnnualDays { get; set; } = 30;
}

public partial class LeaveBalance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int LeaveTypeId { get; set; }
    public int Year { get; set; }
    public decimal Days { get; set; }
}

public partial class LeaveRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int LeaveTypeId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "Pending";
}

public partial class PayrollPeriod
{
    public int Id { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int WorkingDays { get; set; }
    public string Status { get; set; } = "Draft";
    public int Revision { get; set; }
}

public partial class PayrollLine
{
    public int Id { get; set; }
    public int PayrollPeriodId { get; set; }
    public int EmployeeId { get; set; }
    public decimal Gross { get; set; }
    public decimal AttendanceCalculated { get; set; }
    public decimal AttendanceApplied { get; set; }
    public decimal UnpaidLeave { get; set; }
    public decimal Net { get; set; }
    public string Currency { get; set; } = "USD";
}

public partial class Payslip
{
    public int Id { get; set; }
    public int PayrollLineId { get; set; }
    public bool IsPublished { get; set; }
}

public partial class AuditLog
{
    public int Id { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}
