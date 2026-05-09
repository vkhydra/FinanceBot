using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpensePlanningSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EhEssencial",
                table: "Transacoes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EhFixo",
                table: "Transacoes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EhEssencial",
                table: "Transacoes");

            migrationBuilder.DropColumn(
                name: "EhFixo",
                table: "Transacoes");
        }
    }
}
