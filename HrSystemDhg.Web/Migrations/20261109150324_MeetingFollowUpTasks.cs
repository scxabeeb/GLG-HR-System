using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class MeetingFollowUpTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceMeetingId",
                table: "WorkTasks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_SourceMeetingId",
                table: "WorkTasks",
                column: "SourceMeetingId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkTasks_WorkMeetings_SourceMeetingId",
                table: "WorkTasks",
                column: "SourceMeetingId",
                principalTable: "WorkMeetings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkTasks_WorkMeetings_SourceMeetingId",
                table: "WorkTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkTasks_SourceMeetingId",
                table: "WorkTasks");

            migrationBuilder.DropColumn(
                name: "SourceMeetingId",
                table: "WorkTasks");
        }
    }
}
