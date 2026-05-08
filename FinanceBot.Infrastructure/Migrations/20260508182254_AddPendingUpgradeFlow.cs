using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingUpgradeFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpgradeSolicitadoEmUtc",
                table: "AssinaturasUsuario",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpgradeSolicitadoEmUtc",
                table: "AssinaturasUsuario");
        }
    }
}
