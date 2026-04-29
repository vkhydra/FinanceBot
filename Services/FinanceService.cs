using System.Globalization;
using FinanceBot.Data;
using FinanceBot.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceBot.Services;

public class FinanceService
{
        private readonly AppDbContext _db;

        public FinanceService(AppDbContext db)
        {
                _db = db;
        }

        public async Task<(bool Sucesso, string Mensagem)> ProcessarMensagem(string corpoMensagem)
        {
                string comando = corpoMensagem.Trim().ToLower();
                return comando switch
                {
                        "total" => await ObterResumoFinanceiro(),
                        "listar" => await ListarUltimosRegistros(),
                        "desfazer" => await DesfazerUltimaAcao(),
                        "comandos" or "ajuda" or "help" => ObterMenuAjuda(),
                        _ => await IdentificarERegistrar(corpoMensagem)
                };
        }
        private (bool Valido, string Descricao, decimal Valor, bool EhFixo) ParseDados(string mensagem)
        {
                try
                {
                        // Verifica se a mensagem termina com "fixo"
                        bool ehFixo = mensagem.ToLower().EndsWith("fixo");
                        string texto = ehFixo ? mensagem.Substring(0, mensagem.Length - 4).Trim() : mensagem.Trim();

                        // Encontra o divisor entre descrição e valor (último espaço)
                        int ultimoEspaco = texto.LastIndexOf(' ');
                        if (ultimoEspaco == -1) return (false, "", 0, false);

                        string descricao = texto.Substring(0, ultimoEspaco).Trim();
                        string valorStr = texto.Substring(ultimoEspaco + 1).Replace(",", ".");

                        if (decimal.TryParse(valorStr, CultureInfo.InvariantCulture, out decimal valor))
                                return (true, descricao, valor, ehFixo);

                        return (false, "", 0, false);
                }
                catch { return (false, "", 0, false); }
        }
        private async Task<(bool Sucesso, string Mensagem)> RegistrarGasto(string mensagem)
        {
                var (valido, desc, valor, _) = ParseDados(mensagem);
                if (!valido) return (false, "Formato: 'Descrição Valor'");

                _db.Transacoes.Add(new Transacao { Id = Guid.NewGuid(), Descricao = desc, Valor = valor, Data = DateTime.UtcNow });
                await _db.SaveChangesAsync();
                return (true, $"📉 Gasto: {desc} (R$ {valor:N2})");
        }

        private async Task<(bool Sucesso, string Mensagem)> RegistrarReceita(string mensagem)
        {
                // Remove o prefixo (+ ou ganho) antes de processar
                string limpa = mensagem.Replace("+", "").Replace("ganho", "", StringComparison.OrdinalIgnoreCase).Trim();

                var (valido, desc, valor, ehFixo) = ParseDados(limpa);
                if (!valido) return (false, "Formato: 'Descrição Valor' (opcional: fixo)");

                _db.Receitas.Add(new Receita { Id = Guid.NewGuid(), Descricao = desc, Valor = valor, Data = DateTime.UtcNow, EhFixo = ehFixo });
                await _db.SaveChangesAsync();
                return (true, $"📈 Receita: {desc} (R$ {valor:N2}) {(ehFixo ? "[Fixo]" : "")}");
        }
        private async Task<(bool Sucesso, string Mensagem)> IdentificarERegistrar(string mensagem)
        {
                string msg = mensagem.ToLower();

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
                        return (false, $"Desculpe, não entendi.\n\n{menu.Mensagem}");
                }

                return resultado;
        }
        private async Task<(bool Sucesso, string Mensagem)> ObterResumoFinanceiro()
        {
                var hoje = DateTime.UtcNow.Date;

                var gastos = await _db.Transacoes.Where(t => t.Data.Date == hoje).SumAsync(t => t.Valor);
                var ganhos = await _db.Receitas.Where(r => r.Data.Date == hoje).SumAsync(r => r.Valor);

                return (true, $"💰 *Resumo de Hoje*\n📈 Ganho: R$ {ganhos:N2}\n📉 Gasto: R$ {gastos:N2}\n⚖️ *Saldo: R$ {(ganhos - gastos):N2}*");
        }
        private async Task<(bool Sucesso, string Mensagem)> ListarUltimosRegistros()
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

                if (!todos.Any()) return (true, "Nenhum registro encontrado.");

                var listaFormatada = string.Join("\n", todos.Select(x => $"{x.Tipo} {x.Desc}: R$ {x.Valor:N2}"));

                return (true, $"📝 *Últimos Movimentos:*\n\n{listaFormatada}");
        }
        private async Task<(bool Sucesso, string Mensagem)> DesfazerUltimaAcao()
        {
                // Busca o último de cada
                var ultimoGasto = await _db.Transacoes.OrderByDescending(t => t.Data).FirstOrDefaultAsync();
                var ultimaReceita = await _db.Receitas.OrderByDescending(r => r.Data).FirstOrDefaultAsync();

                if (ultimoGasto == null && ultimaReceita == null)
                        return (false, "Nada para desfazer.");

                // Descobre qual é o mais recente dos dois
                if (ultimaReceita != null && (ultimoGasto == null || ultimaReceita.Data > ultimoGasto.Data))
                {
                        _db.Receitas.Remove(ultimaReceita);
                        await _db.SaveChangesAsync();
                        return (true, $"🚫 Desfeito: Receita '{ultimaReceita.Descricao}' removida.");
                }
                else
                {
                        _db.Transacoes.Remove(ultimoGasto!);
                        await _db.SaveChangesAsync();
                        return (true, $"🚫 Desfeito: Gasto '{ultimoGasto!.Descricao}' removido.");
                }
        }

        private (bool Sucesso, string Mensagem) ObterMenuAjuda()
        {
                return (true, "🤖 *FinanceBot - Comandos*\n\n" +
                              "• *Descricao Valor*: Gasto\n" +
                              "• *+ Descricao Valor*: Ganho\n" +
                              "• *Total*: Saldo do dia\n" +
                              "• *Listar*: Últimos registros\n" +
                              "• *Desfazer*: Apaga o último");
        }
}