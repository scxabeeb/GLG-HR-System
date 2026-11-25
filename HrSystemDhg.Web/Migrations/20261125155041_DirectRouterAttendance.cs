using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class DirectRouterAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DirectCloudEnabled",
                table: "ZkDevices",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DirectCloudPublicAddress",
                table: "ZkDevices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DirectCloudEnabled",
                table: "ZkDevices");

            migrationBuilder.DropColumn(
                name: "DirectCloudPublicAddress",
                table: "ZkDevices");
        }
    }
}
