using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceWindowCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CreditFullDayForValidPunches",
                table: "WorkRules",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StatusMessage",
                table: "AttendanceDays",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"
                INSERT INTO WorkRules (EffectiveFrom, DailyHours, StartHour, GraceMinutes, BreakMinutes,
                    BranchId, DepartmentId, ShiftId, StartMinute, WorkingWeekdays, IncompletePolicy,
                    OvertimeMultiplier, WeekendOvertime, UsePunchWindows, CheckInFrom, CheckInUntil,
                    CheckOutFrom, CheckOutUntil, MinuteRateMode, SalaryDivisor, FixedMinuteRate, CreditFullDayForValidPunches)
                SELECT '2026-11-24', 8, 6, 0, 0, BranchId, DepartmentId, ShiftId, 30,
                    '0,1,2,3,4,6', 'Absent', OvertimeMultiplier, WeekendOvertime, 1,
                    '06:30:00','10:30:00','12:30:00','15:30:00',MinuteRateMode,SalaryDivisor,FixedMinuteRate,1
                FROM (SELECT w.*, ROW_NUMBER() OVER (PARTITION BY BranchId,DepartmentId,ShiftId
                    ORDER BY EffectiveFrom DESC,Id DESC) AS policy_rank FROM WorkRules w
                    WHERE EffectiveFrom <= '2026-11-24') AS current_rules
                WHERE policy_rank = 1 AND UsePunchWindows = 1;");
            migrationBuilder.Sql(@"INSERT INTO AuditLogs (Actor, Action, At)
                VALUES ('System policy update', 'Attendance window credit effective 2026-11-24: valid 06:30–10:30 check-in and 12:30–15:30 checkout credit 8 hours; original rule versions and raw punches preserved.', UTC_TIMESTAMP());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreditFullDayForValidPunches",
                table: "WorkRules");

            migrationBuilder.DropColumn(
                name: "StatusMessage",
                table: "AttendanceDays");
        }
    }
}
