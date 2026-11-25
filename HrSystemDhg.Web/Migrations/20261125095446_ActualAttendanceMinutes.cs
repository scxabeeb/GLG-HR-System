using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class ActualAttendanceMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CalculationVersion",
                table: "AttendanceDays",
                type: "int",
                nullable: false,
                defaultValue: 0);
            // Append effective versions; retain previous configuration and saved payroll revisions.
            migrationBuilder.Sql(@"
                INSERT INTO WorkRules (EffectiveFrom, DailyHours, StartHour, GraceMinutes, BreakMinutes,
                    BranchId, DepartmentId, ShiftId, StartMinute, WorkingWeekdays, IncompletePolicy,
                    OvertimeMultiplier, WeekendOvertime, UsePunchWindows, CheckInFrom, CheckInUntil,
                    CheckOutFrom, CheckOutUntil, MinuteRateMode, SalaryDivisor, FixedMinuteRate, CreditFullDayForValidPunches)
                SELECT '2026-11-01', DailyHours, StartHour, GraceMinutes, BreakMinutes,
                    BranchId, DepartmentId, ShiftId, StartMinute, WorkingWeekdays, IncompletePolicy,
                    OvertimeMultiplier, WeekendOvertime, UsePunchWindows, CheckInFrom, CheckInUntil,
                    CheckOutFrom, CheckOutUntil, MinuteRateMode, SalaryDivisor, FixedMinuteRate, 0
                FROM (SELECT w.*, ROW_NUMBER() OVER (PARTITION BY BranchId,DepartmentId,ShiftId
                    ORDER BY EffectiveFrom DESC,Id DESC) AS policy_rank FROM WorkRules w
                    WHERE EffectiveFrom <= '2026-11-01') AS current_rules
                WHERE policy_rank = 1 AND UsePunchWindows = 1 AND CreditFullDayForValidPunches = 1;");
            migrationBuilder.Sql(@"
                INSERT INTO WorkRules (EffectiveFrom, DailyHours, StartHour, GraceMinutes, BreakMinutes,
                    BranchId, DepartmentId, ShiftId, StartMinute, WorkingWeekdays, IncompletePolicy,
                    OvertimeMultiplier, WeekendOvertime, UsePunchWindows, CheckInFrom, CheckInUntil,
                    CheckOutFrom, CheckOutUntil, MinuteRateMode, SalaryDivisor, FixedMinuteRate, CreditFullDayForValidPunches)
                SELECT EffectiveFrom, DailyHours, StartHour, GraceMinutes, BreakMinutes,
                    BranchId, DepartmentId, ShiftId, StartMinute, WorkingWeekdays, IncompletePolicy,
                    OvertimeMultiplier, WeekendOvertime, UsePunchWindows, CheckInFrom, CheckInUntil,
                    CheckOutFrom, CheckOutUntil, MinuteRateMode, SalaryDivisor, FixedMinuteRate, 0
                FROM (SELECT w.*, ROW_NUMBER() OVER (PARTITION BY BranchId,DepartmentId,ShiftId,EffectiveFrom
                    ORDER BY Id DESC) AS policy_rank FROM WorkRules w
                    WHERE EffectiveFrom >= '2026-11-01') AS later_rules
                WHERE policy_rank = 1 AND UsePunchWindows = 1 AND CreditFullDayForValidPunches = 1;");
            migrationBuilder.Sql(@"INSERT INTO AuditLogs (Actor,Action,At) VALUES ('System policy update',
                'Actual attendance minutes from November 2026: use time out minus time in; shortfall against required hours; basic / 26 / 480 for an eight-hour day. Previous rule versions, punches and saved payroll amounts retained.',UTC_TIMESTAMP());");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalculationVersion",
                table: "AttendanceDays");
        }
    }
}
