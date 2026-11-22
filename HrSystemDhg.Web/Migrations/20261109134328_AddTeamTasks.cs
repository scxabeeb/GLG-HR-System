using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Responsibility",
                table: "WorkTasks",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "WorkTasks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkTaskTeams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Title = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OwnerId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTaskTeams", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TeamId",
                table: "WorkTasks",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_WorkTaskTeams_TeamId",
                table: "WorkTasks",
                column: "TeamId",
                principalTable: "WorkTaskTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_WorkTaskTeams_TeamId",
                table: "WorkTasks");

            migrationBuilder.DropTable(
                name: "WorkTaskTeams");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_TeamId",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "Responsibility",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "WorkTasks");
        }
    }
}
