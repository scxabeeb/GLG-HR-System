using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc/>
    public partial class CompleteHrWorkflows : Migration
    {
        /// <inheritdoc/>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "BranchId", table: "WorkRules", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "DepartmentId", table: "WorkRules", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "IncompletePolicy", table: "WorkRules", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<decimal>(name: "OvertimeMultiplier", table: "WorkRules", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<int>(name: "ShiftId", table: "WorkRules", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "StartMinute", table: "WorkRules", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<bool>(name: "WeekendOvertime", table: "WorkRules", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "WorkingWeekdays", table: "WorkRules", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<string>(name: "Country", table: "WorkExperiences", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<string>(name: "Reference", table: "WorkExperiences", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<string>(name: "Responsibilities", table: "WorkExperiences", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<string>(name: "AccountName", table: "SalaryVersions", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<decimal>(name: "Housing", table: "SalaryVersions", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<string>(name: "OtherAllowanceName", table: "SalaryVersions", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<bool>(name: "OvertimeEligible", table: "SalaryVersions", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<decimal>(name: "Transport", table: "SalaryVersions", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<int>(name: "BranchId", table: "PayrollPeriods", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "SourceHash", table: "PayrollPeriods", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<decimal>(name: "AllowanceTotal", table: "PayrollLines", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(name: "Basic", table: "PayrollLines", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<int>(name: "BranchId", table: "PayrollLines", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "DepartmentId", table: "PayrollLines", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<decimal>(name: "NetOverride", table: "PayrollLines", type: "decimal(65,30)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "OverrideReason", table: "PayrollLines", type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<decimal>(name: "Overtime", table: "PayrollLines", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<int>(name: "Revision", table: "PayrollLines", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "SubDepartmentId", table: "PayrollLines", type: "int", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "AccrueMonthly", table: "LeaveTypes", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "EmploymentRestriction", table: "LeaveTypes", type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<bool>(name: "EnforceBalance", table: "LeaveTypes", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "GenderRestriction", table: "LeaveTypes", type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<int>(name: "MaxDays", table: "LeaveTypes", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "MinDays", table: "LeaveTypes", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<bool>(name: "RequiresDocument", table: "LeaveTypes", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(name: "DecidedAt", table: "LeaveRequests", type: "datetime(6)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "DecidedBy", table: "LeaveRequests", type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<string>(name: "DecisionReason", table: "LeaveRequests", type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<int>(name: "DocumentId", table: "LeaveRequests", type: "int", nullable: true);
            migrationBuilder.AddColumn<TimeSpan>(name: "EndTime", table: "LeaveRequests", type: "time(6)", nullable: true);
            migrationBuilder.AddColumn<TimeSpan>(name: "StartTime", table: "LeaveRequests", type: "time(6)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Address", table: "Employees", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<DateTime>(name: "DateOfBirth", table: "Employees", type: "datetime(6)", nullable: true);
            migrationBuilder.AddColumn<int>(name: "Dependents", table: "Employees", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "EmergencyContact", table: "Employees", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<string>(name: "EmergencyPhone", table: "Employees", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<string>(name: "EmergencyRelation", table: "Employees", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<string>(name: "EmploymentType", table: "Employees", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<string>(name: "Gender", table: "Employees", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<int>(name: "ManagerId", table: "Employees", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "MaritalStatus", table: "Employees", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<string>(name: "Nationality", table: "Employees", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<string>(name: "PersonalEmail", table: "Employees", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<bool>(name: "RegistrationComplete", table: "Employees", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "ContentType", table: "EmployeeDocuments", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<string>(name: "OriginalName", table: "EmployeeDocuments", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<DateTime>(name: "ContractEnd", table: "EmployeeAssignments", type: "datetime(6)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Grade", table: "EmployeeAssignments", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<int>(name: "ShiftId", table: "EmployeeAssignments", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Grade", table: "Educations", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<int>(name: "StartYear", table: "Educations", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "HeadId", table: "Departments", type: "int", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "RequiresSubDepartment", table: "Departments", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<int>(name: "BranchId", table: "DeductionRules", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Address", table: "Branchs", type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<string>(name: "CorrectionReason", table: "AttendanceDays", type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<int>(name: "EarlyMinutes", table: "AttendanceDays", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<decimal>(name: "ExpectedHours", table: "AttendanceDays", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<bool>(name: "IsCorrected", table: "AttendanceDays", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "IsEnabled", table: "AspNetUsers", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.CreateTable(name: "CorrectionRequests", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), EmployeeId = table.Column<int>(type: "int", nullable: false), AttendanceDate = table.Column<DateTime>(type: "datetime(6)", nullable: true), Details = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Status = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Resolution = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_CorrectionRequests", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "Holidays", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), Date = table.Column<DateTime>(type: "datetime(6)", nullable: false), Name = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), BranchId = table.Column<int>(type: "int", nullable: true) }, constraints: table =>
            {
                table.PrimaryKey("PK_Holidays", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "OvertimeApprovals", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), EmployeeId = table.Column<int>(type: "int", nullable: false), Date = table.Column<DateTime>(type: "datetime(6)", nullable: false), Hours = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), Reason = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Actor = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_OvertimeApprovals", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "PayrollDeductions", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), PayrollLineId = table.Column<int>(type: "int", nullable: false), Date = table.Column<DateTime>(type: "datetime(6)", nullable: false), Type = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), Calculated = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), Applied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false), Reason = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_PayrollDeductions", x => x.Id);
                table.ForeignKey(name: "FK_PayrollDeductions_PayrollLines_PayrollLineId", column: x => x.PayrollLineId, principalTable: "PayrollLines", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "Settings", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), AllowFutureHires = table.Column<bool>(type: "tinyint(1)", nullable: false), ManagementBankAccess = table.Column<bool>(type: "tinyint(1)", nullable: false), ManagerApproval = table.Column<bool>(type: "tinyint(1)", nullable: false), CompanyName = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4") }, constraints: table =>
            {
                table.PrimaryKey("PK_Settings", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "Shifts", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), Name = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_Shifts", x => x.Id);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateTable(name: "UserBranchScopes", columns: table => new { Id = table.Column<int>(type: "int", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn), UserId = table.Column<string>(type: "varchar(255)", nullable: false).Annotation("MySql:CharSet", "utf8mb4"), BranchId = table.Column<int>(type: "int", nullable: false) }, constraints: table =>
            {
                table.PrimaryKey("PK_UserBranchScopes", x => x.Id);
                table.ForeignKey(name: "FK_UserBranchScopes_AspNetUsers_UserId", column: x => x.UserId, principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateIndex(name: "IX_SalaryVersions_EmployeeId_EffectiveFrom", table: "SalaryVersions", columns: new[] { "EmployeeId", "EffectiveFrom" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_Payslips_PayrollLineId", table: "Payslips", column: "PayrollLineId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_PayrollLines_PayrollPeriodId_EmployeeId_Revision", table: "PayrollLines", columns: new[] { "PayrollPeriodId", "EmployeeId", "Revision" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_LeaveBalances_EmployeeId_LeaveTypeId_Year", table: "LeaveBalances", columns: new[] { "EmployeeId", "LeaveTypeId", "Year" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_EmployeeAssignments_BranchId", table: "EmployeeAssignments", column: "BranchId");
            migrationBuilder.CreateIndex(name: "IX_EmployeeAssignments_DepartmentId", table: "EmployeeAssignments", column: "DepartmentId");
            migrationBuilder.CreateIndex(name: "IX_EmployeeAssignments_SubDepartmentId", table: "EmployeeAssignments", column: "SubDepartmentId");
            migrationBuilder.CreateIndex(name: "IX_PayrollDeductions_PayrollLineId", table: "PayrollDeductions", column: "PayrollLineId");
            migrationBuilder.CreateIndex(name: "IX_UserBranchScopes_UserId_BranchId", table: "UserBranchScopes", columns: new[] { "UserId", "BranchId" }, unique: true);
            migrationBuilder.AddForeignKey(name: "FK_EmployeeAssignments_Branchs_BranchId", table: "EmployeeAssignments", column: "BranchId", principalTable: "Branchs", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_EmployeeAssignments_Departments_DepartmentId", table: "EmployeeAssignments", column: "DepartmentId", principalTable: "Departments", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_EmployeeAssignments_SubDepartments_SubDepartmentId", table: "EmployeeAssignments", column: "SubDepartmentId", principalTable: "SubDepartments", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Payslips_PayrollLines_PayrollLineId", table: "Payslips", column: "PayrollLineId", principalTable: "PayrollLines", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.DropIndex(name: "IX_SalaryVersions_EmployeeId", table: "SalaryVersions");
            migrationBuilder.DropIndex(name: "IX_PayrollLines_PayrollPeriodId_EmployeeId", table: "PayrollLines");
            migrationBuilder.DropIndex(name: "IX_LeaveBalances_EmployeeId", table: "LeaveBalances");
            // Preserve the behavior of records created by the initial application.
            migrationBuilder.Sql("UPDATE AspNetUsers SET IsEnabled = 1");
            migrationBuilder.Sql("UPDATE Employees SET RegistrationComplete = 1");
            migrationBuilder.Sql("UPDATE WorkRules SET WorkingWeekdays = '1,2,3,4,5', IncompletePolicy = 'Flag', OvertimeMultiplier = 1.5");
            migrationBuilder.Sql("UPDATE PayrollLines l JOIN PayrollPeriods p ON p.Id=l.PayrollPeriodId SET l.Revision=p.Revision, l.Basic=l.Gross");
        }

        /// <inheritdoc/>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(name: "IX_SalaryVersions_EmployeeId", table: "SalaryVersions", column: "EmployeeId");
            migrationBuilder.CreateIndex(name: "IX_PayrollLines_PayrollPeriodId_EmployeeId", table: "PayrollLines", columns: new[] { "PayrollPeriodId", "EmployeeId" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_LeaveBalances_EmployeeId", table: "LeaveBalances", column: "EmployeeId");
            migrationBuilder.DropForeignKey(name: "FK_EmployeeAssignments_Branchs_BranchId", table: "EmployeeAssignments");
            migrationBuilder.DropForeignKey(name: "FK_EmployeeAssignments_Departments_DepartmentId", table: "EmployeeAssignments");
            migrationBuilder.DropForeignKey(name: "FK_EmployeeAssignments_SubDepartments_SubDepartmentId", table: "EmployeeAssignments");
            migrationBuilder.DropForeignKey(name: "FK_Payslips_PayrollLines_PayrollLineId", table: "Payslips");
            migrationBuilder.DropTable(name: "CorrectionRequests");
            migrationBuilder.DropTable(name: "Holidays");
            migrationBuilder.DropTable(name: "OvertimeApprovals");
            migrationBuilder.DropTable(name: "PayrollDeductions");
            migrationBuilder.DropTable(name: "Settings");
            migrationBuilder.DropTable(name: "Shifts");
            migrationBuilder.DropTable(name: "UserBranchScopes");
            migrationBuilder.DropIndex(name: "IX_SalaryVersions_EmployeeId_EffectiveFrom", table: "SalaryVersions");
            migrationBuilder.DropIndex(name: "IX_Payslips_PayrollLineId", table: "Payslips");
            migrationBuilder.DropIndex(name: "IX_PayrollLines_PayrollPeriodId_EmployeeId_Revision", table: "PayrollLines");
            migrationBuilder.DropIndex(name: "IX_LeaveBalances_EmployeeId_LeaveTypeId_Year", table: "LeaveBalances");
            migrationBuilder.DropIndex(name: "IX_EmployeeAssignments_BranchId", table: "EmployeeAssignments");
            migrationBuilder.DropIndex(name: "IX_EmployeeAssignments_DepartmentId", table: "EmployeeAssignments");
            migrationBuilder.DropIndex(name: "IX_EmployeeAssignments_SubDepartmentId", table: "EmployeeAssignments");
            migrationBuilder.DropColumn(name: "BranchId", table: "WorkRules");
            migrationBuilder.DropColumn(name: "DepartmentId", table: "WorkRules");
            migrationBuilder.DropColumn(name: "IncompletePolicy", table: "WorkRules");
            migrationBuilder.DropColumn(name: "OvertimeMultiplier", table: "WorkRules");
            migrationBuilder.DropColumn(name: "ShiftId", table: "WorkRules");
            migrationBuilder.DropColumn(name: "StartMinute", table: "WorkRules");
            migrationBuilder.DropColumn(name: "WeekendOvertime", table: "WorkRules");
            migrationBuilder.DropColumn(name: "WorkingWeekdays", table: "WorkRules");
            migrationBuilder.DropColumn(name: "Country", table: "WorkExperiences");
            migrationBuilder.DropColumn(name: "Reference", table: "WorkExperiences");
            migrationBuilder.DropColumn(name: "Responsibilities", table: "WorkExperiences");
            migrationBuilder.DropColumn(name: "AccountName", table: "SalaryVersions");
            migrationBuilder.DropColumn(name: "Housing", table: "SalaryVersions");
            migrationBuilder.DropColumn(name: "OtherAllowanceName", table: "SalaryVersions");
            migrationBuilder.DropColumn(name: "OvertimeEligible", table: "SalaryVersions");
            migrationBuilder.DropColumn(name: "Transport", table: "SalaryVersions");
            migrationBuilder.DropColumn(name: "BranchId", table: "PayrollPeriods");
            migrationBuilder.DropColumn(name: "SourceHash", table: "PayrollPeriods");
            migrationBuilder.DropColumn(name: "AllowanceTotal", table: "PayrollLines");
            migrationBuilder.DropColumn(name: "Basic", table: "PayrollLines");
            migrationBuilder.DropColumn(name: "BranchId", table: "PayrollLines");
            migrationBuilder.DropColumn(name: "DepartmentId", table: "PayrollLines");
            migrationBuilder.DropColumn(name: "NetOverride", table: "PayrollLines");
            migrationBuilder.DropColumn(name: "OverrideReason", table: "PayrollLines");
            migrationBuilder.DropColumn(name: "Overtime", table: "PayrollLines");
            migrationBuilder.DropColumn(name: "Revision", table: "PayrollLines");
            migrationBuilder.DropColumn(name: "SubDepartmentId", table: "PayrollLines");
            migrationBuilder.DropColumn(name: "AccrueMonthly", table: "LeaveTypes");
            migrationBuilder.DropColumn(name: "EmploymentRestriction", table: "LeaveTypes");
            migrationBuilder.DropColumn(name: "EnforceBalance", table: "LeaveTypes");
            migrationBuilder.DropColumn(name: "GenderRestriction", table: "LeaveTypes");
            migrationBuilder.DropColumn(name: "MaxDays", table: "LeaveTypes");
            migrationBuilder.DropColumn(name: "MinDays", table: "LeaveTypes");
            migrationBuilder.DropColumn(name: "RequiresDocument", table: "LeaveTypes");
            migrationBuilder.DropColumn(name: "DecidedAt", table: "LeaveRequests");
            migrationBuilder.DropColumn(name: "DecidedBy", table: "LeaveRequests");
            migrationBuilder.DropColumn(name: "DecisionReason", table: "LeaveRequests");
            migrationBuilder.DropColumn(name: "DocumentId", table: "LeaveRequests");
            migrationBuilder.DropColumn(name: "EndTime", table: "LeaveRequests");
            migrationBuilder.DropColumn(name: "StartTime", table: "LeaveRequests");
            migrationBuilder.DropColumn(name: "Address", table: "Employees");
            migrationBuilder.DropColumn(name: "DateOfBirth", table: "Employees");
            migrationBuilder.DropColumn(name: "Dependents", table: "Employees");
            migrationBuilder.DropColumn(name: "EmergencyContact", table: "Employees");
            migrationBuilder.DropColumn(name: "EmergencyPhone", table: "Employees");
            migrationBuilder.DropColumn(name: "EmergencyRelation", table: "Employees");
            migrationBuilder.DropColumn(name: "EmploymentType", table: "Employees");
            migrationBuilder.DropColumn(name: "Gender", table: "Employees");
            migrationBuilder.DropColumn(name: "ManagerId", table: "Employees");
            migrationBuilder.DropColumn(name: "MaritalStatus", table: "Employees");
            migrationBuilder.DropColumn(name: "Nationality", table: "Employees");
            migrationBuilder.DropColumn(name: "PersonalEmail", table: "Employees");
            migrationBuilder.DropColumn(name: "RegistrationComplete", table: "Employees");
            migrationBuilder.DropColumn(name: "ContentType", table: "EmployeeDocuments");
            migrationBuilder.DropColumn(name: "OriginalName", table: "EmployeeDocuments");
            migrationBuilder.DropColumn(name: "ContractEnd", table: "EmployeeAssignments");
            migrationBuilder.DropColumn(name: "Grade", table: "EmployeeAssignments");
            migrationBuilder.DropColumn(name: "ShiftId", table: "EmployeeAssignments");
            migrationBuilder.DropColumn(name: "Grade", table: "Educations");
            migrationBuilder.DropColumn(name: "StartYear", table: "Educations");
            migrationBuilder.DropColumn(name: "HeadId", table: "Departments");
            migrationBuilder.DropColumn(name: "RequiresSubDepartment", table: "Departments");
            migrationBuilder.DropColumn(name: "BranchId", table: "DeductionRules");
            migrationBuilder.DropColumn(name: "Address", table: "Branchs");
            migrationBuilder.DropColumn(name: "CorrectionReason", table: "AttendanceDays");
            migrationBuilder.DropColumn(name: "EarlyMinutes", table: "AttendanceDays");
            migrationBuilder.DropColumn(name: "ExpectedHours", table: "AttendanceDays");
            migrationBuilder.DropColumn(name: "IsCorrected", table: "AttendanceDays");
            migrationBuilder.DropColumn(name: "IsEnabled", table: "AspNetUsers");
        }
    }
}
