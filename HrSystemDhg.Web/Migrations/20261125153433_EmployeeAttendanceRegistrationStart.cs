using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeAttendanceRegistrationStart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RegisteredUtc",
                table: "Employees",
                type: "datetime(6)",
                nullable: true);
            migrationBuilder.Sql(@"UPDATE Employees e SET RegisteredUtc =
                (SELECT MIN(a.At) FROM AuditLogs a WHERE a.Action = CONCAT('Registered employee ', e.EmployeeNumber))
                WHERE EXISTS (SELECT 1 FROM AuditLogs a WHERE a.Action = CONCAT('Registered employee ', e.EmployeeNumber));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegisteredUtc",
                table: "Employees");
        }
    }
}
