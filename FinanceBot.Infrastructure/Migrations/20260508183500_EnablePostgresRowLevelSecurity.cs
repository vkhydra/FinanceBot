using FinanceBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceBot.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260508183500_EnablePostgresRowLevelSecurity")]
    public partial class EnablePostgresRowLevelSecurity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Transacoes" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "Transacoes" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS transacoes_usuario_rls ON "Transacoes";
                CREATE POLICY transacoes_usuario_rls ON "Transacoes"
                    USING ("UsuarioId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid)
                    WITH CHECK ("UsuarioId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                ALTER TABLE "Receitas" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "Receitas" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS receitas_usuario_rls ON "Receitas";
                CREATE POLICY receitas_usuario_rls ON "Receitas"
                    USING ("UsuarioId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid)
                    WITH CHECK ("UsuarioId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS transacoes_usuario_rls ON "Transacoes";
                ALTER TABLE "Transacoes" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "Transacoes" DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS receitas_usuario_rls ON "Receitas";
                ALTER TABLE "Receitas" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "Receitas" DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
