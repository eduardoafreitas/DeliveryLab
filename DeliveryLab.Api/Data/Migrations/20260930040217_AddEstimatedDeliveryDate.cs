using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEstimatedDeliveryDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EstimatedDeliveryDate",
                table: "Deliveries",
                type: "date",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Deliveries",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "EstimatedDeliveryDate",
                value: null);

            migrationBuilder.UpdateData(
                table: "Deliveries",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "EstimatedDeliveryDate",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedDeliveryDate",
                table: "Deliveries");
        }
    }
}
