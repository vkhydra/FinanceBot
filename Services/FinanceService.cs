using System.Globalization;
using FinanceBot.Data;
using FinanceBot.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceBot.Services;

public class FinanceService : IFinanceMessageProcessor
{
        private readonly AppDbContext _db;

        public FinanceService(AppDbContext db)
        {
                _db = db;
        }

        public async Task<FinanceMessageResult> ProcessarMensagemAsync(FinanceMessageRequest request)
        {
                string comando = request.CorpoMensagem.Trim().ToLowerInvariant();
                return comando switch
                {
                        "total" => await ObterResumoFinanceiro(),
                        "listar" => await ListarUltimosRegistros(),
                        "desfazer" => await DesfazerUltimaAcao(),
                        "comandos" or "ajuda" or "help" => ObterMenuAjuda(),
                        _ => await IdentificarERegistrar(request.CorpoMensagem)
                };
        }

        private (bool Valido, string Descricao, decimal Valor, bool EhFixo) ParseDados(string mensagem)
        {
                bool ehFixo = mensagem.EndsWith("fixo", StringComparison.OrdinalIgnoreCase);
                string texto = ehFixo ? mensagem[..^4].Trim() : mensagem.Trim();

                int ultimoEspaco = texto.LastIndexOf(' ');
                if (ultimoEspaco <= 0 || ultimoEspaco == texto.Length - 1)
                {
                        return (false, "", 0, false);
                }

                string descricao = texto[..ultimoEspaco].Trim();
                string valorStr = texto[(ultimoEspaco + 1)..].Replace(",", ".");

                if (string.IsNullOrWhiteSpace(descricao))
                {
                        return (false, "", 0, false);
                }

                if (decimal.TryParse(valorStr, CultureInfo.InvariantCulture, out decimal valor))
                {
                        return (true, descricao, valor, ehFixo);
                }

                return (false, "", 0, false);
        }

        private async Task<FinanceMessageResult> RegistrarGasto(string mensagem)
        {
                var (valido, desc, valor, _) = ParseDados(mensagem);
                if (!valido) return FinanceMessageResult.Falha("Formato: 'Descrição Valor'");

                _db.Transacoes.Add(new Transacao { Id = Guid.NewGuid(), Descricao = desc, Valor = valor, Data = DateTime.UtcNow });
                await _db.SaveChangesAsync();
                return FinanceMessageResult.Ok($"📉 Gasto: {desc} (R$ {valor:N2})");
        }

        private async Task<FinanceMessageResult> RegistrarReceita(string mensagem)
        {
                // Remove o prefixo (+ ou ganho) antes de processar
                string limpa = mensagem.Replace("+", "").Replace("ganho", "", StringComparison.OrdinalIgnoreCase).Trim();

                var (valido, desc, valor, ehFixo) = ParseDados(limpa);
                if (!valido) return FinanceMessageResult.Falha("Formato: 'Descrição Valor' (opcional: fixo)");

                _db.Receitas.Add(new Receita { Id = Guid.NewGuid(), Descricao = desc, Valor = valor, Data = DateTime.UtcNow, EhFixo = ehFixo });
                await _db.SaveChangesAsync();
                return FinanceMessageResult.Ok($"📈 Receita: {desc} (R$ {valor:N2}) {(ehFixo ? "[Fixo]" : "")}");
        }

        private async Task<FinanceMessageResult> IdentificarERegistrar(string mensagem)
        {
                string msg = mensagem.ToLowerInvariant();

                // Se começa com + ou a palavra ganho/receita, vai para a tabela de Receitas
                if (msg.StartsWith("+") || msg.StartsWith("ganho") || msg.StartsWith("receita"))
                {
                        return await RegistrarReceita(mensagem);
                }

                // Caso contrário, tenta registrar como Gasto
                var resultado = await RegistrarGasto(mensagem);

                if (!resultado.Sucesso)
                {
                        var menu = ObterMenuAjuda();
                        return FinanceMessageResult.Falha($"Desculpe, não entendi.\n\n{menu.Mensagem}");
                }

                return resultado;
        }

        private async Task<FinanceMessageResult> ObterResumoFinanceiro()
        {
                var hoje = DateTime.UtcNow.Date;

                var gastos = await _db.Transacoes.Where(t => t.Data.Date == hoje).SumAsync(t => t.Valor);
                var ganhos = await _db.Receitas.Where(r => r.Data.Date == hoje).SumAsync(r => r.Valor);

                return FinanceMessageResult.Ok($"💰 *Resumo de Hoje*\n📈 Ganho: R$ {ganhos:N2}\n📉 Gasto: R$ {gastos:N2}\n⚖️ *Saldo: R$ {(ganhos - gastos):N2}*");
        }

        private async Task<FinanceMessageResult> ListarUltimosRegistros()
        {
                // Busca os 5 últimos de cada tabela
                var ultimosGastos = await _db.Transacoes.OrderByDescending(t => t.Data).Take(5).ToListAsync();
                var ultimasReceitas = await _db.Receitas.OrderByDescending(r => r.Data).Take(5).ToListAsync();

                // Une as duas listas e ordena pela data mais recente
                var todos = ultimosGastos.Select(g => new { Tipo = "📉", Desc = g.Descricao, Valor = g.Valor, Data = g.Data })
                    .Union(ultimasReceitas.Select(r => new { Tipo = "📈", Desc = r.Descricao, Valor = r.Valor, Data = r.Data }))
                    .OrderByDescending(x => x.Data)
                    .Take(5)
                    .ToList();

                if (!todos.Any()) return FinanceMessageResult.Ok("Nenhum registro encontrado.");

                var listaFormatada = string.Join("\n", todos.Select(x => $"{x.Tipo} {x.Desc}: R$ {x.Valor:N2}"));

                return FinanceMessageResult.Ok($"📝 *Últimos Movimentos:*\n\n{listaFormatada}");
        }

        private async Task<FinanceMessageResult> DesfazerUltimaAcao()
        {
                // Busca o último de cada
                var ultimoGasto = await _db.Transacoes.OrderByDescending(t => t.Data).FirstOrDefaultAsync();
                var ultimaReceita = await _db.Receitas.OrderByDescending(r => r.Data).FirstOrDefaultAsync();

                if (ultimoGasto == null && ultimaReceita == null)
                        return FinanceMessageResult.Falha("Nada para desfazer.");

                // Descobre qual é o mais recente dos dois
                if (ultimaReceita != null && (ultimoGasto == null || ultimaReceita.Data > ultimoGasto.Data))
                {
                        _db.Receitas.Remove(ultimaReceita);
                        await _db.SaveChangesAsync();
                        return FinanceMessageResult.Ok($"🚫 Desfeito: Receita '{ultimaReceita.Descricao}' removida.");
                }
                else
                {
                        _db.Transacoes.Remove(ultimoGasto!);
                        await _db.SaveChangesAsync();
                        return FinanceMessageResult.Ok($"🚫 Desfeito: Gasto '{ultimoGasto!.Descricao}' removido.");
                }
        }

        private FinanceMessageResult ObterMenuAjuda()
        {
                return FinanceMessageResult.Ok("🤖 *FinanceBot - Comandos*\n\n" +
                                               "• *Descricao Valor*: Gasto\n" +
                                               "• *+ Descricao Valor*: Ganho\n" +
                                               "• *Total*: Saldo do dia\n" +
                                               "• *Listar*: Últimos registros\n" +
                                               "• *Desfazer*: Apaga o último");
        }
}
