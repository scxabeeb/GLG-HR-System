using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class DeviceDepartmentLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "ZkDevices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "ZkDevices",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ZkDevices_DepartmentId",
                table: "ZkDevices",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ZkDevices_Departments_DepartmentId",
                table: "ZkDevices",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZkDevices_Departments_DepartmentId",
                table: "ZkDevices");

            migrationBuilder.DropIndex(
                name: "IX_ZkDevices_DepartmentId",
                table: "ZkDevices");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "ZkDevices");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "ZkDevices");
        }
    }
}
