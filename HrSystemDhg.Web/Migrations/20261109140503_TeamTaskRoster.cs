using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class TeamTaskRoster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Brief",
                table: "WorkTaskTeams",
                type: "varchar(2500)",
                maxLength: 2500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "SubtaskId",
                table: "WorkTasks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkSubtasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    Responsibility = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSubtasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkSubtasks_WorkTaskTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "WorkTaskTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WorkTaskTeamMembers",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTaskTeamMembers", x => new { x.TeamId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkTaskTeamMembers_WorkTaskTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "WorkTaskTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("""
                INSERT INTO WorkTaskTeamMembers (TeamId, UserId)
                SELECT DISTINCT TeamId, AssigneeId FROM WorkTasks
                WHERE TeamId IS NOT NULL AND AssigneeId <> ''
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_SubtaskId",
                table: "WorkTasks",
                column: "SubtaskId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSubtasks_TeamId",
                table: "WorkSubtasks",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTaskTeamMembers_UserId",
                table: "WorkTaskTeamMembers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_WorkSubtasks_SubtaskId",
                table: "WorkTasks",
                column: "SubtaskId",
                principalTable: "WorkSubtasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_WorkSubtasks_SubtaskId",
                table: "WorkTasks");

            migrationBuilder.DropTable(
                name: "WorkSubtasks");

            migrationBuilder.DropTable(
                name: "WorkTaskTeamMembers");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_SubtaskId",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "Brief",
                table: "WorkTaskTeams");

            migrationBuilder.DropColumn(
                name: "SubtaskId",
                table: "WorkTasks");
        }
    }
}
