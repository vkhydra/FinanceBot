using FinanceBot.Application.Contracts;
using FinanceBot.Domain.Entities;
using FinanceBot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceBot.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserContext _currentUserContext;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : this(options, AnonymousCurrentUserContext.Instance)
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserContext currentUserContext)
        : base(options)
    {
        _currentUserContext = currentUserContext;
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<AssinaturaUsuario> AssinaturasUsuario => Set<AssinaturaUsuario>();
    public DbSet<Transacao> Transacoes => Set<Transacao>();
    public DbSet<Receita> Receitas => Set<Receita>();

    private Guid CurrentUsuarioId => _currentUserContext.UsuarioId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(usuario => usuario.Id);
            entity.Property(usuario => usuario.Email).IsRequired().HasMaxLength(320);
            entity.Property(usuario => usuario.SenhaHash).IsRequired();
            entity.Property(usuario => usuario.CodigoVinculo).HasMaxLength(6);
            entity.HasIndex(usuario => usuario.Email).IsUnique();
            entity.HasIndex(usuario => usuario.TelegramId).IsUnique();
            entity.HasIndex(usuario => usuario.CodigoVinculo).IsUnique();
            entity.HasOne(usuario => usuario.Assinatura)
                .WithOne(assinatura => assinatura.Usuario)
                .HasForeignKey<AssinaturaUsuario>(assinatura => assinatura.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssinaturaUsuario>(entity =>
        {
            entity.HasKey(assinatura => assinatura.Id);
            entity.Property(assinatura => assinatura.PlanoAtual)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(assinatura => assinatura.StatusAssinatura)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(assinatura => assinatura.GatewayCustomerId).HasMaxLength(200);
            entity.Property(assinatura => assinatura.GatewaySubscriptionId).HasMaxLength(200);
            entity.HasIndex(assinatura => assinatura.UsuarioId).IsUnique();
        });

        modelBuilder.Entity<Transacao>(entity =>
        {
            entity.HasKey(transacao => transacao.Id);
            entity.Property(transacao => transacao.Descricao).IsRequired();
            entity.Property(transacao => transacao.Categoria).HasMaxLength(100);
            entity.HasIndex(transacao => new { transacao.UsuarioId, transacao.Data });
            entity.HasOne(transacao => transacao.Usuario)
                .WithMany(usuario => usuario.Transacoes)
                .HasForeignKey(transacao => transacao.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(transacao => transacao.UsuarioId == CurrentUsuarioId);
        });

        modelBuilder.Entity<Receita>(entity =>
        {
            entity.HasKey(receita => receita.Id);
            entity.Property(receita => receita.Descricao).IsRequired();
            entity.HasIndex(receita => new { receita.UsuarioId, receita.Data });
            entity.HasOne(receita => receita.Usuario)
                .WithMany(usuario => usuario.Receitas)
                .HasForeignKey(receita => receita.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(receita => receita.UsuarioId == CurrentUsuarioId);
        });
    }

    private sealed class AnonymousCurrentUserContext : ICurrentUserContext
    {
        public static AnonymousCurrentUserContext Instance { get; } = new();

        public Guid? UsuarioId => null;
        public string? Email => null;
        public long? TelegramChatId => null;
        public bool IsAuthenticated => false;
    }
}
