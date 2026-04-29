namespace FinanceBot.Data;

using Microsoft.EntityFrameworkCore;
using FinanceBot.Models;

public class AppDbContext : DbContext
{
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Transacao> Transacoes { get; set; }
        public DbSet<Receita> Receitas { get; set; }
}