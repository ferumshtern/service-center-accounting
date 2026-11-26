using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceCenter.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWarrantyClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarrantyClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    WarrantyOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarrantyClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarrantyClaims_Orders_OriginalOrderId",
                        column: x => x.OriginalOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarrantyClaims_Orders_WarrantyOrderId",
                        column: x => x.WarrantyOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaims_OriginalOrderId",
                table: "WarrantyClaims",
                column: "OriginalOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyClaims_WarrantyOrderId",
                table: "WarrantyClaims",
                column: "WarrantyOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarrantyClaims");
        }
    }
}
