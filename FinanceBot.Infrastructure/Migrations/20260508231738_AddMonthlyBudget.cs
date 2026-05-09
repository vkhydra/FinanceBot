using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrcamentosMensais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ano = table.Column<int>(type: "integer", nullable: false),
                    Mes = table.Column<int>(type: "integer", nullable: false),
                    LimiteGastos = table.Column<decimal>(type: "numeric", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrcamentosMensais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrcamentosMensais_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrcamentosMensais_UsuarioId_Ano_Mes",
                table: "OrcamentosMensais",
                columns: new[] { "UsuarioId", "Ano", "Mes" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE "OrcamentosMensais" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "OrcamentosMensais" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS orcamentos_mensais_usuario_rls ON "OrcamentosMensais";
                CREATE POLICY orcamentos_mensais_usuario_rls ON "OrcamentosMensais"
                    USING ("UsuarioId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid)
                    WITH CHECK ("UsuarioId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS orcamentos_mensais_usuario_rls ON "OrcamentosMensais";
                ALTER TABLE "OrcamentosMensais" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "OrcamentosMensais" DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "OrcamentosMensais");
        }
    }
}
