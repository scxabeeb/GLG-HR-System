using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendancePush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastPushSeenUtc",
                table: "ZkDevices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PushAddress",
                table: "ZkDevices",
                type: "varchar(45)",
                maxLength: 45,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PushSerial",
                table: "ZkDevices",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PushStamp",
                table: "ZkDevices",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ZkDevices_PushSerial",
                table: "ZkDevices",
                column: "PushSerial",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ZkDevices_PushSerial",
                table: "ZkDevices");

            migrationBuilder.DropColumn(
                name: "LastPushSeenUtc",
                table: "ZkDevices");

            migrationBuilder.DropColumn(
                name: "PushAddress",
                table: "ZkDevices");

            migrationBuilder.DropColumn(
                name: "PushSerial",
                table: "ZkDevices");

            migrationBuilder.DropColumn(
                name: "PushStamp",
                table: "ZkDevices");
        }
    }
}
