using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class InPersonMeetingMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "WorkMeetings",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MinutesDraft",
                table: "WorkMeetings",
                type: "text",
                maxLength: 12000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "MinutesPublishedUtc",
                table: "WorkMeetings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedMinutes",
                table: "WorkMeetings",
                type: "text",
                maxLength: 12000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Location",
                table: "WorkMeetings");

            migrationBuilder.DropColumn(
                name: "MinutesDraft",
                table: "WorkMeetings");

            migrationBuilder.DropColumn(
                name: "MinutesPublishedUtc",
                table: "WorkMeetings");

            migrationBuilder.DropColumn(
                name: "PublishedMinutes",
                table: "WorkMeetings");
        }
    }
}
