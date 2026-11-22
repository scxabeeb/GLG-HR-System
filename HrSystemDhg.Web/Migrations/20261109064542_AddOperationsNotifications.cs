using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationsNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedUtc",
                table: "WorkTasks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OperationsNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RecipientId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Progress = table.Column<int>(type: "int", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReadUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TaskId = table.Column<int>(type: "int", nullable: true),
                    MeetingId = table.Column<int>(type: "int", nullable: true),
                    WhatsApp = table.Column<int>(type: "int", nullable: false),
                    AttemptUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationsNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationsNotifications_AspNetUsers_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperationsNotifications_WorkMeetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "WorkMeetings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OperationsNotifications_WorkTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "WorkTasks",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WhatsAppPreferences",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Number = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OptedInUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_WhatsAppPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OperationsNotifications_MeetingId",
                table: "OperationsNotifications",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationsNotifications_RecipientId_ReadUtc_CreatedUtc",
                table: "OperationsNotifications",
                columns: new[] { "RecipientId", "ReadUtc", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationsNotifications_TaskId",
                table: "OperationsNotifications",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationsNotifications_WhatsApp_Id",
                table: "OperationsNotifications",
                columns: new[] { "WhatsApp", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationsNotifications");

            migrationBuilder.DropTable(
                name: "WhatsAppPreferences");

            migrationBuilder.DropColumn(
                name: "UpdatedUtc",
                table: "WorkTasks");
        }
    }
}
