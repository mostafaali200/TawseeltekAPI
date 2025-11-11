using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddPenaltyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "DriverID",
                table: "Penalties",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Penalties",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Penalties",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PassengerID",
                table: "Penalties",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PenaltyType",
                table: "Penalties",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Penalties",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Penalties_PassengerID",
                table: "Penalties",
                column: "PassengerID");

            migrationBuilder.AddForeignKey(
                name: "FK_Penalties_Passengers_PassengerID",
                table: "Penalties",
                column: "PassengerID",
                principalTable: "Passengers",
                principalColumn: "PassengerID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Penalties_Passengers_PassengerID",
                table: "Penalties");

            migrationBuilder.DropIndex(
                name: "IX_Penalties_PassengerID",
                table: "Penalties");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Penalties");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Penalties");

            migrationBuilder.DropColumn(
                name: "PassengerID",
                table: "Penalties");

            migrationBuilder.DropColumn(
                name: "PenaltyType",
                table: "Penalties");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Penalties");

            migrationBuilder.AlterColumn<int>(
                name: "DriverID",
                table: "Penalties",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
