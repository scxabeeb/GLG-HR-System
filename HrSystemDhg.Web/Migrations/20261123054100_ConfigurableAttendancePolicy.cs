using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurableAttendancePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "CheckInFrom",
                table: "WorkRules",
                type: "time(6)",
                nullable: false,
                defaultValue: new TimeSpan(6, 30, 0));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "CheckInUntil",
                table: "WorkRules",
                type: "time(6)",
                nullable: false,
                defaultValue: new TimeSpan(10, 30, 0));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "CheckOutFrom",
                table: "WorkRules",
                type: "time(6)",
                nullable: false,
                defaultValue: new TimeSpan(11, 30, 0));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "CheckOutUntil",
                table: "WorkRules",
                type: "time(6)",
                nullable: false,
                defaultValue: new TimeSpan(15, 30, 0));

            migrationBuilder.AddColumn<decimal>(
                name: "FixedMinuteRate",
                table: "WorkRules",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MinuteRateMode",
                table: "WorkRules",
                type: "longtext",
                nullable: false,
                defaultValue: "Salary")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryDivisor",
                table: "WorkRules",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 30m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckInFrom",
                table: "WorkRules");

            migrationBuilder.DropColumn(
                name: "CheckInUntil",
                table: "WorkRules");

            migrationBuilder.DropColumn(
                name: "CheckOutFrom",
                table: "WorkRules");

            migrationBuilder.DropColumn(
                name: "CheckOutUntil",
                table: "WorkRules");

            migrationBuilder.DropColumn(
                name: "FixedMinuteRate",
                table: "WorkRules");

            migrationBuilder.DropColumn(
                name: "MinuteRateMode",
                table: "WorkRules");

            migrationBuilder.DropColumn(
                name: "SalaryDivisor",
                table: "WorkRules");
        }
    }
}
