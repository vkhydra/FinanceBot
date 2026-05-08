using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssinaturasUsuario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanoAtual = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusAssinatura = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PremiumAteUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrialAteUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GatewayCustomerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GatewaySubscriptionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssinaturasUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssinaturasUsuario_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasUsuario_UsuarioId",
                table: "AssinaturasUsuario",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "AssinaturasUsuario" (
                    "Id",
                    "UsuarioId",
                    "PlanoAtual",
                    "StatusAssinatura",
                    "PremiumAteUtc",
                    "TrialAteUtc",
                    "GatewayCustomerId",
                    "GatewaySubscriptionId",
                    "AtualizadoEmUtc"
                )
                SELECT
                    "Id",
                    "Id",
                    'Free',
                    'Nenhuma',
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    NOW()
                FROM "Usuarios" u
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "AssinaturasUsuario" a
                    WHERE a."UsuarioId" = u."Id"
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssinaturasUsuario");
        }
    }
}
