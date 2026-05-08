using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnrichRecordMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Observacao",
                table: "Transacoes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origem",
                table: "Transacoes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Observacao",
                table: "Receitas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origem",
                table: "Receitas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Observacao",
                table: "Transacoes");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "Transacoes");

            migrationBuilder.DropColumn(
                name: "Observacao",
                table: "Receitas");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "Receitas");
        }
    }
}
