using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DeliveryLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackingCode = table.Column<string>(type: "text", nullable: false),
                    RecipientName = table.Column<string>(type: "text", nullable: false),
                    OriginAddress = table.Column<string>(type: "text", nullable: false),
                    DestinationAddress = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Deliveries",
                columns: new[] { "Id", "CreatedAt", "DestinationAddress", "OriginAddress", "RecipientName", "Status", "TrackingCode", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Campinas - SP", "São Paulo - SP", "Cliente Teste 1", "Created", "DL-11111111111111111111111111111111", null },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Utc), "Ribeirão Preto - SP", "São José do Rio Preto - SP", "Cliente Teste 2", "Created", "DL-22222222222222222222222222222222", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_TrackingCode",
                table: "Deliveries",
                column: "TrackingCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deliveries");
        }
    }
}
