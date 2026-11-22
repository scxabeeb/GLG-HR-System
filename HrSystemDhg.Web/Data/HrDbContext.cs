using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HrSystemDhg.Web.Data;
public class HrDbContext(DbContextOptions<HrDbContext> options, IHttpContextAccessor? http = null) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<UserModuleRestriction> UserModuleRestrictions => Set<UserModuleRestriction>();
    public DbSet<OperationsNotification> OperationsNotifications => Set<OperationsNotification>();
    public DbSet<WhatsAppPreference> WhatsAppPreferences => Set<WhatsAppPreference>();
    public DbSet<WorkTaskTeamMember> WorkTaskTeamMembers => Set<WorkTaskTeamMember>();
    public DbSet<WorkSubtask> WorkSubtasks => Set<WorkSubtask>();
    public DbSet<WorkTaskTeam> WorkTaskTeams => Set<WorkTaskTeam>();
    public DbSet<TaskActivity> TaskActivities => Set<TaskActivity>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<WorkMeeting> WorkMeetings => Set<WorkMeeting>();
    public DbSet<MeetingAgendaItem> MeetingAgendaItems => Set<MeetingAgendaItem>();
    public DbSet<AgendaItem> AgendaItems => Set<AgendaItem>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetPhoto> AssetPhotos => Set<AssetPhoto>();
    public DbSet<AssetActivity> AssetActivities => Set<AssetActivity>();
    public DbSet<FleetTrip> FleetTrips => Set<FleetTrip>();
    public DbSet<FleetExpense> FleetExpenses => Set<FleetExpense>();
    public DbSet<FleetVehicle> FleetVehicles => Set<FleetVehicle>();
    public DbSet<FleetPosition> FleetPositions => Set<FleetPosition>();
    public DbSet<FleetServiceEntry> FleetServiceEntries => Set<FleetServiceEntry>();
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
    public DbSet<ZkDeviceCommand> ZkDeviceCommands => Set<ZkDeviceCommand>();
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
    public bool EmployeeOnly => http?.HttpContext?.User.IsInRole("Employee") == true && !IsStaff &&
        !((http?.HttpContext?.GetRouteValue("area")?.ToString() == "Hr" && http.HttpContext.GetRouteValue("page")?.ToString() == "/Leave") &&
          (http.HttpContext.User.IsInRole("General Secretary")));
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
        b.Entity<MeetingAgendaItem>().HasOne<WorkMeeting>().WithMany().HasForeignKey(x => x.MeetingId);
        b.Entity<AgendaItem>().HasIndex(x => new { x.OwnerId, x.Completed, x.Date });
        b.Entity<AgendaItem>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId);
        b.Entity<Asset>().Property(x => x.PurchaseCost).HasPrecision(18, 2);
        b.Entity<Asset>().Property(x => x.BookValue).HasPrecision(18, 2);
        b.Entity<AssetActivity>().Property(x => x.BookValue).HasPrecision(18, 2);
        b.Entity<Asset>().HasIndex(x => x.Tag).IsUnique();
        b.Entity<Asset>().HasIndex(x => new { x.BranchId, x.Status });
        b.Entity<Asset>().HasQueryFilter(x => !Scoped || AllowedBranchIds.Contains(x.BranchId));
        b.Entity<Asset>().HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Asset>().HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Asset>().HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Asset>().HasOne(x => x.Photo).WithOne().HasForeignKey<AssetPhoto>(x => x.AssetId);
        b.Entity<AssetActivity>().HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId);
        b.Entity<AssetActivity>().HasQueryFilter(x => !Scoped || AllowedBranchIds.Contains(x.BranchId));
        b.Entity<AssetActivity>().HasIndex(x => new { x.AssetId, x.RecordedUtc });
        b.Entity<FleetTrip>().HasIndex(t => new { t.VehicleId, t.Status, t.StartsUtc });
        b.Entity<FleetTrip>().HasIndex(t => new { t.DriverId, t.Status, t.StartsUtc });
        b.Entity<FleetTrip>().HasIndex(t => new { t.BranchId, t.StartsUtc });
        b.Entity<FleetTrip>().HasIndex(t => t.RequesterId);
        b.Entity<FleetTrip>().HasOne(t => t.Vehicle).WithMany().HasForeignKey(t => t.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FleetTrip>().HasOne(t => t.Driver).WithMany().HasForeignKey(t => t.DriverId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FleetExpense>().HasOne(t => t.Vehicle).WithMany().HasForeignKey(t => t.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FleetExpense>().HasIndex(t => new { t.BranchId, t.Date });
        b.Entity<WorkTaskTeamMember>().HasKey(m => new { m.TeamId, m.UserId });
        b.Entity<WorkTaskTeamMember>().HasIndex(m => m.UserId);
        b.Entity<WorkTaskTeam>().HasMany(t => t.Members).WithOne().HasForeignKey(m => m.TeamId);
        b.Entity<WorkSubtask>().HasOne<WorkTaskTeam>().WithMany().HasForeignKey(s => s.TeamId);
        b.Entity<WorkTask>().HasOne(t => t.Subtask).WithMany().HasForeignKey(t => t.SubtaskId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WorkTask>().HasOne(t => t.Team).WithMany().HasForeignKey(t => t.TeamId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TaskActivity>().HasOne(a => a.Task).WithMany().HasForeignKey(a => a.TaskId);
        b.Entity<TaskActivity>().HasIndex(a => new { a.TaskId, a.RecordedUtc });
        b.Entity<UserModuleRestriction>().HasKey(r => new { r.UserId, r.Module });
        b.Entity<UserModuleRestriction>().HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.UserId);
        b.Entity<ZkDevice>().HasOne<Department>().WithMany()
            .HasForeignKey(d => d.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ZkDevice>().HasIndex(d => d.PushSerial).IsUnique();
        b.Entity<OperationsNotification>().HasIndex(n => new { n.RecipientId, n.ReadUtc, n.CreatedUtc });
        b.Entity<OperationsNotification>().HasIndex(n => new { n.WhatsApp, n.Id });
        b.Entity<WhatsAppPreference>().HasKey(p => p.UserId);
        b.Entity<WhatsAppPreference>().HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.UserId);
        b.Entity<OperationsNotification>().HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.RecipientId);
        b.Entity<WorkTask>().HasOne<WorkMeeting>().WithMany().HasForeignKey(t => t.SourceMeetingId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<WorkTask>().HasIndex(t => t.OwnerId);
        b.Entity<WorkTask>().HasIndex(t => t.AssigneeId);
        b.Entity<WorkMeeting>().HasIndex(m => m.OwnerId);
        b.Entity<MeetingParticipant>().HasKey(p => new { p.MeetingId, p.UserId });
        b.Entity<MeetingParticipant>().HasIndex(p => p.UserId);
        b.Entity<WorkMeeting>().HasMany(m => m.Participants).WithOne().HasForeignKey(p => p.MeetingId);
        b.Entity<FleetVehicle>().HasIndex(v => v.Plate).IsUnique();
        b.Entity<FleetVehicle>().HasOne<Branch>().WithMany().HasForeignKey(v => v.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FleetVehicle>().HasOne<Employee>().WithMany().HasForeignKey(v => v.DriverId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FleetPosition>().HasOne<FleetVehicle>().WithMany().HasForeignKey(p => p.VehicleId);
        b.Entity<FleetPosition>().HasIndex(p => new { p.VehicleId, p.RecordedUtc }).IsUnique();
        b.Entity<FleetServiceEntry>().HasOne<FleetVehicle>().WithMany().HasForeignKey(p => p.VehicleId);
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
