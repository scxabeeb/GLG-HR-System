using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceMinuteWindows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UsePunchWindows",
                table: "WorkRules",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MissingMinutes",
                table: "AttendanceDays",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // New effective versions retain history and cover every existing organization scope.
            migrationBuilder.Sql("""
                INSERT INTO WorkRules (EffectiveFrom, BranchId, DepartmentId, ShiftId, StartHour, StartMinute,
                    DailyHours, BreakMinutes, GraceMinutes, WorkingWeekdays, IncompletePolicy,
                    OvertimeMultiplier, WeekendOvertime, UsePunchWindows)
                SELECT '2026-11-23', BranchId, DepartmentId, ShiftId, 6, 30, 8, 0, 0, '0,1,2,3,4,6',
                    'Flag', OvertimeMultiplier, WeekendOvertime, 1
                FROM (SELECT w.*, ROW_NUMBER() OVER (PARTITION BY BranchId, DepartmentId, ShiftId
                    ORDER BY EffectiveFrom DESC, Id DESC) AS rule_rank FROM WorkRules w
                    WHERE EffectiveFrom <= '2026-11-23') current_rules WHERE rule_rank = 1;
                """);
            migrationBuilder.Sql("""
                INSERT INTO WorkRules (EffectiveFrom, StartHour, StartMinute, DailyHours, BreakMinutes,
                    GraceMinutes, WorkingWeekdays, IncompletePolicy, OvertimeMultiplier, WeekendOvertime, UsePunchWindows)
                SELECT '2026-11-23', 6, 30, 8, 0, 0, '0,1,2,3,4,6', 'Flag', 1.5, 0, 1
                WHERE NOT EXISTS (SELECT 1 FROM WorkRules WHERE BranchId IS NULL AND DepartmentId IS NULL
                    AND ShiftId IS NULL AND EffectiveFrom <= '2026-11-23');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsePunchWindows",
                table: "WorkRules");

            migrationBuilder.DropColumn(
                name: "MissingMinutes",
                table: "AttendanceDays");
        }
    }
}
