using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class DeviceBranchOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "ZkDevices",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"UPDATE ZkDevices AS device
                INNER JOIN Departments AS department ON department.Id = device.DepartmentId
                SET device.BranchId = department.BranchId
                WHERE device.BranchId IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_ZkDevices_BranchId",
                table: "ZkDevices",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_ZkDevices_Branchs_BranchId",
                table: "ZkDevices",
                column: "BranchId",
                principalTable: "Branchs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZkDevices_Branchs_BranchId",
                table: "ZkDevices");

            migrationBuilder.DropIndex(
                name: "IX_ZkDevices_BranchId",
                table: "ZkDevices");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "ZkDevices");
        }
    }
}
