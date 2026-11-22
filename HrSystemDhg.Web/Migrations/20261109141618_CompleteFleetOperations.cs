using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemDhg.Web.Migrations
{
    /// <inheritdoc />
    public partial class CompleteFleetOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FleetTripId",
                table: "OperationsNotifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InspectionDue",
                table: "FleetVehicles",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InsuranceExpires",
                table: "FleetVehicles",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationExpires",
                table: "FleetVehicles",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FleetExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Litres = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    Reference = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetExpenses_FleetVehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "FleetVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FleetTrips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    RequesterId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VehicleId = table.Column<int>(type: "int", nullable: true),
                    DriverId = table.Column<int>(type: "int", nullable: true),
                    Destination = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Purpose = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartsUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndsUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DepartedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReturnedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    StartKm = table.Column<int>(type: "int", nullable: true),
                    EndKm = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecisionNote = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetTrips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetTrips_Employees_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetTrips_FleetVehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "FleetVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OperationsNotifications_FleetTripId",
                table: "OperationsNotifications",
                column: "FleetTripId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetExpenses_BranchId_Date",
                table: "FleetExpenses",
                columns: new[] { "BranchId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetExpenses_VehicleId",
                table: "FleetExpenses",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_BranchId_StartsUtc",
                table: "FleetTrips",
                columns: new[] { "BranchId", "StartsUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_DriverId_Status_StartsUtc",
                table: "FleetTrips",
                columns: new[] { "DriverId", "Status", "StartsUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_RequesterId",
                table: "FleetTrips",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_VehicleId_Status_StartsUtc",
                table: "FleetTrips",
                columns: new[] { "VehicleId", "Status", "StartsUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_OperationsNotifications_FleetTrips_FleetTripId",
                table: "OperationsNotifications",
                column: "FleetTripId",
                principalTable: "FleetTrips",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperationsNotifications_FleetTrips_FleetTripId",
                table: "OperationsNotifications");

            migrationBuilder.DropTable(
                name: "FleetExpenses");

            migrationBuilder.DropTable(
                name: "FleetTrips");

            migrationBuilder.DropIndex(
                name: "IX_OperationsNotifications_FleetTripId",
                table: "OperationsNotifications");

            migrationBuilder.DropColumn(
                name: "FleetTripId",
                table: "OperationsNotifications");

            migrationBuilder.DropColumn(
                name: "InspectionDue",
                table: "FleetVehicles");

            migrationBuilder.DropColumn(
                name: "InsuranceExpires",
                table: "FleetVehicles");

            migrationBuilder.DropColumn(
                name: "RegistrationExpires",
                table: "FleetVehicles");
        }
    }
}
