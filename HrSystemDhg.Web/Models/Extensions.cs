namespace HrSystemDhg.Web.Models;
public partial class ApplicationUser
{
    public bool IsEnabled { get; set; } = true;
}

public partial class Employee
{
    public string Gender { get; set; } = "";
    public DateTime? DateOfBirth { get; set; }
    public string Nationality { get; set; } = "";
    public string PersonalEmail { get; set; } = "";
    public string Address { get; set; } = "";
    public string EmergencyContact { get; set; } = "";
    public string EmergencyRelation { get; set; } = "";
    public string EmergencyPhone { get; set; } = "";
    public string MaritalStatus { get; set; } = "";
    public int Dependents { get; set; }
    public string EmploymentType { get; set; } = "Permanent";
    public int? ManagerId { get; set; }
    public bool RegistrationComplete { get; set; } = true;
}

public partial class EmployeeAssignment
{
    public string Grade { get; set; } = "";
    public DateTime? ContractEnd { get; set; }
    public int? ShiftId { get; set; }
}

public partial class Department
{
    public bool RequiresSubDepartment { get; set; }
    public int? HeadId { get; set; }
}

public partial class Branch
{
    public string Address { get; set; } = "";
}

public partial class Education
{
    public int? StartYear { get; set; }
    public string Grade { get; set; } = "";
}

public partial class WorkExperience
{
    public string Country { get; set; } = "";
    public string Responsibilities { get; set; } = "";
    public string Reference { get; set; } = "";
}

public partial class EmployeeDocument
{
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
}

public partial class SalaryVersion
{
    public string AccountName { get; set; } = "";
    public decimal Housing { get; set; }
    public decimal Transport { get; set; }
    public string OtherAllowanceName { get; set; } = "Other";
    public bool OvertimeEligible { get; set; }
}

public partial class WorkRule
{
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int? ShiftId { get; set; }
    public int StartMinute { get; set; }
    public string WorkingWeekdays { get; set; } = "1,2,3,4,5";
    public string IncompletePolicy { get; set; } = "Flag";
    public decimal OvertimeMultiplier { get; set; } = 1.5m;
    public bool WeekendOvertime { get; set; }
}

public partial class DeductionRule
{
    public int? BranchId { get; set; }
}

public partial class AttendanceDay
{
    public decimal ExpectedHours { get; set; }
    public int EarlyMinutes { get; set; }
    public bool IsCorrected { get; set; }
    public string? CorrectionReason { get; set; }
}

public partial class LeaveType
{
    public bool EnforceBalance { get; set; } = true;
    public bool AccrueMonthly { get; set; }
    public bool RequiresDocument { get; set; }
    public int MinDays { get; set; } = 1;
    public int MaxDays { get; set; } = 365;
    public string? GenderRestriction { get; set; }
    public string? EmploymentRestriction { get; set; }
}

public partial class LeaveRequest
{
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public int? DocumentId { get; set; }
    public string? DecisionReason { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public partial class PayrollPeriod
{
    public int? BranchId { get; set; }
    public string SourceHash { get; set; } = "";
}

public partial class PayrollLine
{
    public int Revision { get; set; }
    public int BranchId { get; set; }
    public int DepartmentId { get; set; }
    public int? SubDepartmentId { get; set; }
    public decimal Basic { get; set; }
    public decimal AllowanceTotal { get; set; }
    public decimal Overtime { get; set; }
    public string? OverrideReason { get; set; }
    public decimal? NetOverride { get; set; }
}

public class Shift
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Holiday
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Name { get; set; } = "";
    public int? BranchId { get; set; }
}

public class UserBranchScope
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int BranchId { get; set; }
}

public class PayrollDeduction
{
    public int Id { get; set; }
    public int PayrollLineId { get; set; }
    public DateTime Date { get; set; }
    public string Type { get; set; } = "";
    public decimal Calculated { get; set; }
    public decimal Applied { get; set; }
    public string? Reason { get; set; }
}

public class OvertimeApproval
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime Date { get; set; }
    public decimal Hours { get; set; }
    public string Reason { get; set; } = "";
    public string Actor { get; set; } = "";
}

public class CorrectionRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime? AttendanceDate { get; set; }
    public string Details { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string? Resolution { get; set; }
}

public class SystemSettings
{
    public int Id { get; set; } = 1;
    public bool AllowFutureHires { get; set; }
    public bool ManagementBankAccess { get; set; }
    public bool ManagerApproval { get; set; }
    public string CompanyName { get; set; } = "Garowe Local Government";
}
