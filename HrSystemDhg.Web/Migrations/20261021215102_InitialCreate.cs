using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc/>
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc/>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase().Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "AspNetRoles", columns: table => new { Id = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true).Annotation("MySql:CharSet", "utf8mb4"), NormalizedName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true).Annotation("MySql:CharSet", "utf8mb4"), ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_AspNetRoles", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "AuditLogs", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), Actor = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Action = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), At = table.Column<DateTime>(type: "datetime(6)", nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "Branchs", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), Name = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Code = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), City = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Timezone = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_Branchs", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "DeductionRules", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), EffectiveFrom = table.Column<DateTime>(type: "datetime(6)", nullable: false), AbsenceMultiplier = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), LatePerMinute = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_DeductionRules", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "Employees", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), EmployeeNumber = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), FullName = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), WorkEmail = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), NationalId = table.Column<string>(type: "varchar(255)", nullable: true).Annotation("MySql:CharSet", "utf8mb4"), ZkPin = table.Column<string>(type: "varchar(255)", nullable: true).Annotation("MySql:CharSet", "utf8mb4"), Phone = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), HireDate = table.Column<DateTime>(type: "datetime(6)", nullable: false), TerminationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true), Status = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_Employees", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "LeaveTypes", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), Name = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), IsPaid = table.Column<bool>(type: "tinyint(1)", nullable: false), AnnualDays = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_LeaveTypes", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "PayrollPeriods", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), From = table.Column<DateTime>(type: "datetime(6)", nullable: false), To = table.Column<DateTime>(type: "datetime(6)", nullable: false), WorkingDays = table.Column<int>(type: "int", nullable: false), Status = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Revision = table.Column<int>(type: "int", nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_PayrollPeriods", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "Payslips", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), PayrollLineId = table.Column<int>(type: "int", nullable: false), IsPublished = table.Column<bool>(type: "tinyint(1)", nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_Payslips", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "WorkRules", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), EffectiveFrom = table.Column<DateTime>(type: "datetime(6)", nullable: false), DailyHours = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), StartHour = table.Column<int>(type: "int", nullable: false), GraceMinutes = table.Column<int>(type: "int", nullable: false), BreakMinutes = table.Column<int>(type: "int", nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_WorkRules", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "ZkDevices", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), Name = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Endpoint = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), LastSuccess = table.Column<DateTime>(type: "datetime(6)", nullable: true), LastError = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_ZkDevices", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "ZkPunchs", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), DeviceId = table.Column<int>(type: "int", nullable: false), Pin = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), PunchTime = table.Column<DateTime>(type: "datetime(6)", nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_ZkPunchs", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "AspNetRoleClaims", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), RoleId = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), ClaimType = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4"), ClaimValue = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                table.ForeignKey(name: "FK_AspNetRoleClaims_AspNetRoles_RoleId", column: x => x.RoleId, principalTable: "AspNetRoles", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "Departments", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), Name = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Code = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), BranchId = table.Column<int>(type: "int", nullable: false), IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_Departments", x => x.Id);
                table.ForeignKey(name: "FK_Departments_Branchs_BranchId", column: x => x.BranchId, principalTable: "Branchs", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "AspNetUsers", columns: table => new { Id = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), EmployeeId = table.Column<int>(type: "int", nullable: true), MustChangePassword = table.Column<bool>(type: "tinyint(1)", nullable: false), UserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true).Annotation("MySql:CharSet", "utf8mb4"), NormalizedUserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true).Annotation("MySql:CharSet", "utf8mb4"), Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true).Annotation("MySql:CharSet", "utf8mb4"), NormalizedEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true).Annotation("MySql:CharSet", "utf8mb4"), EmailConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false), PasswordHash = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4"), SecurityStamp = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4"), ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4"), PhoneNumber = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4"), PhoneNumberConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false), TwoFactorEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false), LockoutEnd = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true), LockoutEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false), AccessFailedCount = table.Column<int>(type: "int", nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                table.ForeignKey(name: "FK_AspNetUsers_Employees_EmployeeId", column: x => x.EmployeeId, principalTable: "Employees", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "AttendanceDays", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), EmployeeId = table.Column<int>(type: "int", nullable: false), Date = table.Column<DateTime>(type: "datetime(6)", nullable: false), FirstIn = table.Column<DateTime>(type: "datetime(6)", nullable: true), LastOut = table.Column<DateTime>(type: "datetime(6)", nullable: true), WorkedHours = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), LateMinutes = table.Column<int>(type: "int", nullable: false), Status = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceDays", x => x.Id);
                table.ForeignKey(name: "FK_AttendanceDays_Employees_EmployeeId", column: x => x.EmployeeId, principalTable: "Employees", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "Educations", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), EmployeeId = table.Column<int>(type: "int", nullable: false), Institution = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Qualification = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), FieldOfStudy = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), EndYear = table.Column<int>(type: "int", nullable: true) }, constraints: table =>
            {
                table.PrimaryKey("PK_Educations", x => x.Id);
                table.ForeignKey(name: "FK_Educations_Employees_EmployeeId", column: x => x.EmployeeId, principalTable: "Employees", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "EmployeeAssignments", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), EmployeeId = table.Column<int>(type: "int", nullable: false), BranchId = table.Column<int>(type: "int", nullable: false), DepartmentId = table.Column<int>(type: "int", nullable: false), SubDepartmentId = table.Column<int>(type: "int", nullable: true), JobTitle = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), EffectiveFrom = table.Column<DateTime>(type: "datetime(6)", nullable: false), EffectiveTo = table.Column<DateTime>(type: "datetime(6)", nullable: true) }, constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeAssignments", x => x.Id);
                table.ForeignKey(name: "FK_EmployeeAssignments_Employees_EmployeeId", column: x => x.EmployeeId, principalTable: "Employees", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "EmployeeDocuments", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), EmployeeId = table.Column<int>(type: "int", nullable: false), Type = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), StoragePath = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), ExpiryDate = table.Column<DateTime>(type: "datetime(6)", nullable: true) }, constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeDocuments", x => x.Id);
                table.ForeignKey(name: "FK_EmployeeDocuments_Employees_EmployeeId", column: x => x.EmployeeId, principalTable: "Employees", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "LeaveBalances", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), EmployeeId = table.Column<int>(type: "int", nullable: false), LeaveTypeId = table.Column<int>(type: "int", nullable: false), Year = table.Column<int>(type: "int", nullable: false), Days = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_LeaveBalances", x => x.Id);
                table.ForeignKey(name: "FK_LeaveBalances_Employees_EmployeeId", column: x => x.EmployeeId, principalTable: "Employees", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "SalaryVersions", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), EmployeeId = table.Column<int>(type: "int", nullable: false), Basic = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), Allowances = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), Currency = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), BankName = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4"), AccountNumber = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4"), IsDraft = table.Column<bool>(type: "tinyint(1)", nullable: false), EffectiveFrom = table.Column<DateTime>(type: "datetime(6)", nullable: false), EffectiveTo = table.Column<DateTime>(type: "datetime(6)", nullable: true) }, constraints: table =>
            {
                table.PrimaryKey("PK_SalaryVersions", x => x.Id);
                table.ForeignKey(name: "FK_SalaryVersions_Employees_EmployeeId", column: x => x.EmployeeId, principalTable: "Employees", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "WorkExperiences", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), EmployeeId = table.Column<int>(type: "int", nullable: false), Employer = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), JobTitle = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), From = table.Column<DateTime>(type: "datetime(6)", nullable: false), To = table.Column<DateTime>(type: "datetime(6)", nullable: true) }, constraints: table =>
            {
                table.PrimaryKey("PK_WorkExperiences", x => x.Id);
                table.ForeignKey(name: "FK_WorkExperiences_Employees_EmployeeId", column: x => x.EmployeeId, principalTable: "Employees", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "LeaveRequests", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), EmployeeId = table.Column<int>(type: "int", nullable: false), LeaveTypeId = table.Column<int>(type: "int", nullable: false), From = table.Column<DateTime>(type: "datetime(6)", nullable: false), To = table.Column<DateTime>(type: "datetime(6)", nullable: false), Reason = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Status = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                table.ForeignKey(name: "FK_LeaveRequests_Employees_EmployeeId", column: x => x.EmployeeId, principalTable: "Employees", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_LeaveRequests_LeaveTypes_LeaveTypeId", column: x => x.LeaveTypeId, principalTable: "LeaveTypes", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "PayrollLines", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), PayrollPeriodId = table.Column<int>(type: "int", nullable: false), EmployeeId = table.Column<int>(type: "int", nullable: false), Gross = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), AttendanceCalculated = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), AttendanceApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), UnpaidLeave = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), Net = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), Currency = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_PayrollLines", x => x.Id);
                table.ForeignKey(name: "FK_PayrollLines_Employees_EmployeeId", column: x => x.EmployeeId, principalTable: "Employees", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_PayrollLines_PayrollPeriods_PayrollPeriodId", column: x => x.PayrollPeriodId, principalTable: "PayrollPeriods", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "SubDepartments", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), Name = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), DepartmentId = table.Column<int>(type: "int", nullable: false), IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_SubDepartments", x => x.Id);
                table.ForeignKey(name: "FK_SubDepartments_Departments_DepartmentId", column: x => x.DepartmentId, principalTable: "Departments", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "AspNetUserClaims", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), UserId = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), ClaimType = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4"), ClaimValue = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                table.ForeignKey(name: "FK_AspNetUserClaims_AspNetUsers_UserId", column: x => x.UserId, principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "AspNetUserLogins", columns: table => new { LoginProvider = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), ProviderKey = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), ProviderDisplayName = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4"), UserId = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(name: "FK_AspNetUserLogins_AspNetUsers_UserId", column: x => x.UserId, principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "AspNetUserRoles", columns: table => new { UserId = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), RoleId = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(name: "FK_AspNetUserRoles_AspNetRoles_RoleId", column: x => x.RoleId, principalTable: "AspNetRoles", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey(name: "FK_AspNetUserRoles_AspNetUsers_UserId", column: x => x.UserId, principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "AspNetUserTokens", columns: table => new { UserId = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), LoginProvider = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Name = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Value = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(name: "FK_AspNetUserTokens_AspNetUsers_UserId", column: x => x.UserId, principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateIndex(name: "IX_AspNetRoleClaims_RoleId", table: "AspNetRoleClaims", column: "RoleId");
            migrationBuilder.CreateIndex(name: "RoleNameIndex", table: "AspNetRoles", column: "NormalizedName", unique: true);
            migrationBuilder.CreateIndex(name: "IX_AspNetUserClaims_UserId", table: "AspNetUserClaims", column: "UserId");
            migrationBuilder.CreateIndex(name: "IX_AspNetUserLogins_UserId", table: "AspNetUserLogins", column: "UserId");
            migrationBuilder.CreateIndex(name: "IX_AspNetUserRoles_RoleId", table: "AspNetUserRoles", column: "RoleId");
            migrationBuilder.CreateIndex(name: "EmailIndex", table: "AspNetUsers", column: "NormalizedEmail");
            migrationBuilder.CreateIndex(name: "IX_AspNetUsers_EmployeeId", table: "AspNetUsers", column: "EmployeeId");
            migrationBuilder.CreateIndex(name: "UserNameIndex", table: "AspNetUsers", column: "NormalizedUserName", unique: true);
            migrationBuilder.CreateIndex(name: "IX_AttendanceDays_EmployeeId_Date", table: "AttendanceDays", columns: new[] { "EmployeeId", "Date" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_Departments_BranchId", table: "Departments", column: "BranchId");
            migrationBuilder.CreateIndex(name: "IX_Educations_EmployeeId", table: "Educations", column: "EmployeeId");
            migrationBuilder.CreateIndex(name: "IX_EmployeeAssignments_EmployeeId", table: "EmployeeAssignments", column: "EmployeeId");
            migrationBuilder.CreateIndex(name: "IX_EmployeeDocuments_EmployeeId", table: "EmployeeDocuments", column: "EmployeeId");
            migrationBuilder.CreateIndex(name: "IX_Employees_EmployeeNumber", table: "Employees", column: "EmployeeNumber", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Employees_NationalId", table: "Employees", column: "NationalId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Employees_WorkEmail", table: "Employees", column: "WorkEmail", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Employees_ZkPin", table: "Employees", column: "ZkPin", unique: true);
            migrationBuilder.CreateIndex(name: "IX_LeaveBalances_EmployeeId", table: "LeaveBalances", column: "EmployeeId");
            migrationBuilder.CreateIndex(name: "IX_LeaveRequests_EmployeeId", table: "LeaveRequests", column: "EmployeeId");
            migrationBuilder.CreateIndex(name: "IX_LeaveRequests_LeaveTypeId", table: "LeaveRequests", column: "LeaveTypeId");
            migrationBuilder.CreateIndex(name: "IX_PayrollLines_EmployeeId", table: "PayrollLines", column: "EmployeeId");
            migrationBuilder.CreateIndex(name: "IX_PayrollLines_PayrollPeriodId_EmployeeId", table: "PayrollLines", columns: new[] { "PayrollPeriodId", "EmployeeId" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_SalaryVersions_EmployeeId", table: "SalaryVersions", column: "EmployeeId");
            migrationBuilder.CreateIndex(name: "IX_SubDepartments_DepartmentId", table: "SubDepartments", column: "DepartmentId");
            migrationBuilder.CreateIndex(name: "IX_WorkExperiences_EmployeeId", table: "WorkExperiences", column: "EmployeeId");
            migrationBuilder.CreateIndex(name: "IX_ZkPunchs_DeviceId_Pin_PunchTime", table: "ZkPunchs", columns: new[] { "DeviceId", "Pin", "PunchTime" }, unique: true);
        }

        /// <inheritdoc/>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AspNetRoleClaims");
            migrationBuilder.DropTable(name: "AspNetUserClaims");
            migrationBuilder.DropTable(name: "AspNetUserLogins");
            migrationBuilder.DropTable(name: "AspNetUserRoles");
            migrationBuilder.DropTable(name: "AspNetUserTokens");
            migrationBuilder.DropTable(name: "AttendanceDays");
            migrationBuilder.DropTable(name: "AuditLogs");
            migrationBuilder.DropTable(name: "DeductionRules");
            migrationBuilder.DropTable(name: "Educations");
            migrationBuilder.DropTable(name: "EmployeeAssignments");
            migrationBuilder.DropTable(name: "EmployeeDocuments");
            migrationBuilder.DropTable(name: "LeaveBalances");
            migrationBuilder.DropTable(name: "LeaveRequests");
            migrationBuilder.DropTable(name: "PayrollLines");
            migrationBuilder.DropTable(name: "Payslips");
            migrationBuilder.DropTable(name: "SalaryVersions");
            migrationBuilder.DropTable(name: "SubDepartments");
            migrationBuilder.DropTable(name: "WorkExperiences");
            migrationBuilder.DropTable(name: "WorkRules");
            migrationBuilder.DropTable(name: "ZkDevices");
            migrationBuilder.DropTable(name: "ZkPunchs");
            migrationBuilder.DropTable(name: "AspNetRoles");
            migrationBuilder.DropTable(name: "AspNetUsers");
            migrationBuilder.DropTable(name: "LeaveTypes");
            migrationBuilder.DropTable(name: "PayrollPeriods");
            migrationBuilder.DropTable(name: "Departments");
            migrationBuilder.DropTable(name: "Employees");
            migrationBuilder.DropTable(name: "Branchs");
        }
    }
}
