using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Data;
public class HrDbContext(DbContextOptions<HrDbContext> options, IHttpContextAccessor? http = null) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Branch> Branchs => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<SubDepartment> SubDepartments => Set<SubDepartment>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeAssignment> EmployeeAssignments => Set<EmployeeAssignment>();
    public DbSet<Education> Educations => Set<Education>();
    public DbSet<WorkExperience> WorkExperiences => Set<WorkExperience>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<SalaryVersion> SalaryVersions => Set<SalaryVersion>();
    public DbSet<WorkRule> WorkRules => Set<WorkRule>();
    public DbSet<DeductionRule> DeductionRules => Set<DeductionRule>();
    public DbSet<ZkDevice> ZkDevices => Set<ZkDevice>();
    public DbSet<ZkPunch> ZkPunchs => Set<ZkPunch>();
    public DbSet<AttendanceDay> AttendanceDays => Set<AttendanceDay>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollLine> PayrollLines => Set<PayrollLine>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<UserBranchScope> UserBranchScopes => Set<UserBranchScope>();
    public DbSet<PayrollDeduction> PayrollDeductions => Set<PayrollDeduction>();
    public DbSet<OvertimeApproval> OvertimeApprovals => Set<OvertimeApproval>();
    public DbSet<CorrectionRequest> CorrectionRequests => Set<CorrectionRequest>();
    public DbSet<SystemSettings> Settings => Set<SystemSettings>();
    public string? ActorId => http?.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    public bool EmployeeOnly => http?.HttpContext?.User.IsInRole("Employee") == true && !IsStaff;
    public bool IsStaff => http?.HttpContext?.User.IsInRole("Admin") == true || http?.HttpContext?.User.IsInRole("HR") == true || http?.HttpContext?.User.IsInRole("Management") == true;
    public bool IsAdmin => http?.HttpContext?.User.IsInRole("Admin") == true;

    private int[]? branches;
    private int[]? visible;
    public int[] AllowedBranchIds => branches ??= ActorId == null ? [] : UserBranchScopes.Where(x => x.UserId == ActorId).Select(x => x.BranchId).ToArray();
    public bool Scoped => !IsAdmin && ActorId != null && AllowedBranchIds.Length > 0;
    public bool Restricted => EmployeeOnly || Scoped;
    public DateTime ScopeDate => HrSystemDhg.Web.Services.BusinessClock.Today;
    public int[] VisibleEmployeeIds => visible ??= EmployeeOnly ? Users.Where(x => x.Id == ActorId && x.EmployeeId != null).Select(x => x.EmployeeId!.Value).ToArray() : EmployeeAssignments.IgnoreQueryFilters().Where(x => AllowedBranchIds.Contains(x.BranchId) && ((x.EffectiveFrom <= ScopeDate && (x.EffectiveTo == null || x.EffectiveTo >= ScopeDate)) || (x.EffectiveFrom > ScopeDate && !EmployeeAssignments.IgnoreQueryFilters().Any(y => y.EmployeeId == x.EmployeeId && y.EffectiveFrom < x.EffectiveFrom)))).Select(x => x.EmployeeId).Distinct().ToArray();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Employee>().HasIndex(x => x.EmployeeNumber).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.WorkEmail).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.NationalId).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.ZkPin).IsUnique();
        b.Entity<ZkPunch>().HasIndex(x => new { x.DeviceId, x.Pin, x.PunchTime }).IsUnique();
        b.Entity<AttendanceDay>().HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
        b.Entity<PayrollLine>().HasIndex(x => new { x.PayrollPeriodId, x.EmployeeId, x.Revision }).IsUnique();
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
            {
                if (prop.ClrType == typeof(decimal))
                {
                    prop.SetPrecision(18);
                    prop.SetScale(2);
                }
            }

        b.Entity<EmployeeAssignment>().HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Education>().HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WorkExperience>().HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EmployeeDocument>().HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SalaryVersion>().HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AttendanceDay>().HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LeaveRequest>().HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LeaveBalance>().HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PayrollLine>().HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Department>().HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SubDepartment>().HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LeaveRequest>().HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PayrollLine>().HasOne<PayrollPeriod>().WithMany().HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ApplicationUser>().HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Employee>().HasQueryFilter(e => !Restricted || VisibleEmployeeIds.Contains(e.Id));
        b.Entity<EmployeeAssignment>().HasQueryFilter(x => !Restricted || VisibleEmployeeIds.Contains(x.EmployeeId));
        b.Entity<Education>().HasQueryFilter(x => !Restricted || VisibleEmployeeIds.Contains(x.EmployeeId));
        b.Entity<WorkExperience>().HasQueryFilter(x => !Restricted || VisibleEmployeeIds.Contains(x.EmployeeId));
        b.Entity<EmployeeDocument>().HasQueryFilter(x => !Restricted || VisibleEmployeeIds.Contains(x.EmployeeId));
        b.Entity<SalaryVersion>().HasQueryFilter(x => !Restricted || VisibleEmployeeIds.Contains(x.EmployeeId));
        b.Entity<AttendanceDay>().HasQueryFilter(x => !Restricted || VisibleEmployeeIds.Contains(x.EmployeeId));
        b.Entity<LeaveRequest>().HasQueryFilter(x => !Restricted || VisibleEmployeeIds.Contains(x.EmployeeId));
        b.Entity<LeaveBalance>().HasQueryFilter(x => !Restricted || VisibleEmployeeIds.Contains(x.EmployeeId));
        b.Entity<PayrollLine>().HasQueryFilter(x => !Restricted || VisibleEmployeeIds.Contains(x.EmployeeId));
        b.Entity<OvertimeApproval>().HasQueryFilter(x => !Restricted || VisibleEmployeeIds.Contains(x.EmployeeId));
        b.Entity<CorrectionRequest>().HasQueryFilter(x => !Restricted || VisibleEmployeeIds.Contains(x.EmployeeId));
        b.Entity<Branch>().HasQueryFilter(x => !Scoped || UserBranchScopes.Any(s => s.UserId == ActorId && s.BranchId == x.Id));
        b.Entity<Department>().HasQueryFilter(x => Branchs.Any(b => b.Id == x.BranchId));
        b.Entity<SubDepartment>().HasQueryFilter(x => Departments.Any(d => d.Id == x.DepartmentId));
        b.Entity<PayrollPeriod>().HasQueryFilter(x => !Scoped || (x.BranchId != null && UserBranchScopes.Any(s => s.UserId == ActorId && s.BranchId == x.BranchId)));
        b.Entity<SalaryVersion>().HasIndex(x => new { x.EmployeeId, x.EffectiveFrom }).IsUnique();
        b.Entity<LeaveBalance>().HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique();
        b.Entity<Payslip>().HasIndex(x => x.PayrollLineId).IsUnique();
        b.Entity<Payslip>().HasOne<PayrollLine>().WithMany().HasForeignKey(x => x.PayrollLineId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PayrollDeduction>().HasOne<PayrollLine>().WithMany().HasForeignKey(x => x.PayrollLineId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EmployeeAssignment>().HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EmployeeAssignment>().HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EmployeeAssignment>().HasOne<SubDepartment>().WithMany().HasForeignKey(x => x.SubDepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserBranchScope>().HasIndex(x => new { x.UserId, x.BranchId }).IsUnique();
        b.Entity<UserBranchScope>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PayrollDeduction>().HasQueryFilter(x => PayrollLines.Any(l => l.Id == x.PayrollLineId));
        b.Entity<Payslip>().HasQueryFilter(x => PayrollLines.Any(l => l.Id == x.PayrollLineId));
    }
}
