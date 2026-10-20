using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AssignDriverToDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DriverId",
                table: "Deliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Deliveries",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "DriverId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Deliveries",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "DriverId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_DriverId",
                table: "Deliveries",
                column: "DriverId");

            migrationBuilder.AddForeignKey(
                name: "FK_Deliveries_Drivers_DriverId",
                table: "Deliveries",
                column: "DriverId",
                principalTable: "Drivers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deliveries_Drivers_DriverId",
                table: "Deliveries");

            migrationBuilder.DropIndex(
                name: "IX_Deliveries_DriverId",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "Deliveries");
        }
    }
}
