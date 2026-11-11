using System.Diagnostics.CodeAnalysis;
using Bestia.Aplicacao.Modelos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bestia.Infraestrutura.Persistencia;

public sealed class BestiaDbContext(DbContextOptions<BestiaDbContext> opcoes) : DbContext(opcoes)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configuracao)
    {
        configuracao.Properties<Enum>().HaveConversion<byte>();
    }

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Jogador>(e =>
        {
            e.HasIndex(x => x.EmailHash).IsUnique().HasFilter("[EmailHash] IS NOT NULL");
            e.HasIndex(x => x.ApelidoHash).IsUnique().HasFilter("[ApelidoHash] IS NOT NULL");
            e.Property(x => x.Versao).IsRowVersion();
            e.Property(x => x.Idioma).HasMaxLength(10);
            e.Property(x => x.CorDoAvatar).HasMaxLength(20);
            e.Property(x => x.SenhaHash).HasMaxLength(200);
        });
        modelo.Entity<LoginExterno>(e =>
        {
            e.HasIndex(x => new { x.Provedor, x.IdExternoHash }).IsUnique();
            e.Property(x => x.Provedor).HasMaxLength(30);
            e.HasOne<Jogador>().WithMany().HasForeignKey(x => x.JogadorId);
        });
        modelo.Entity<TokenDeConta>(e => e.HasOne<Jogador>().WithMany().HasForeignKey(x => x.JogadorId));
        modelo.Entity<Consentimento>(e =>
        {
            e.Property(x => x.Documento).HasMaxLength(40);
            e.Property(x => x.Versao).HasMaxLength(20);
            e.HasOne<Jogador>().WithMany().HasForeignKey(x => x.JogadorId);
        });
        modelo.Entity<SolicitacaoPrivacidade>(e => e.HasOne<Jogador>().WithMany().HasForeignKey(x => x.JogadorId));
        modelo.Entity<RegistroDeAcesso>(e =>
        {
            e.HasIndex(x => x.OcorreuEm);
            e.HasOne<Jogador>().WithMany().HasForeignKey(x => x.JogadorId);
        });
        modelo.Entity<RegistroDeAuditoria>(e =>
        {
            e.Property(x => x.Acao).HasMaxLength(60);
            e.Property(x => x.Detalhe).HasMaxLength(500);
        });

        modelo.Entity<Sala>(e =>
        {
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.Nome).HasMaxLength(40);
            e.Property(x => x.Codigo).HasMaxLength(6);
            e.Property(x => x.Versao).IsRowVersion();
            e.HasOne<Jogador>().WithMany().HasForeignKey(x => x.CriadaPorJogadorId).OnDelete(DeleteBehavior.NoAction);
        });
        modelo.Entity<SalaJogador>(e =>
        {
            e.HasKey(x => new { x.SalaId, x.JogadorId });
            e.HasOne<Sala>().WithMany().HasForeignKey(x => x.SalaId);
            e.HasOne<Jogador>().WithMany().HasForeignKey(x => x.JogadorId).OnDelete(DeleteBehavior.NoAction);
        });
        modelo.Entity<Partida>(e =>
        {
            e.HasOne<Sala>().WithMany().HasForeignKey(x => x.SalaId);
            e.HasOne<Jogador>().WithMany().HasForeignKey(x => x.VencedorJogadorId).OnDelete(DeleteBehavior.NoAction);
        });
        modelo.Entity<PartidaJogador>(e =>
        {
            e.HasKey(x => new { x.PartidaId, x.Assento });
            e.HasIndex(x => x.JogadorId);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_PartidaJogador_FichasIniciais", "[FichasIniciais] >= 0");
                t.HasCheckConstraint("CK_PartidaJogador_FichasFinais", "[FichasFinais] >= 0");
            });
            e.Property(x => x.NomeDoBot).HasMaxLength(30);
            e.HasOne<Partida>().WithMany().HasForeignKey(x => x.PartidaId);
            e.HasOne<Jogador>().WithMany().HasForeignKey(x => x.JogadorId).OnDelete(DeleteBehavior.NoAction);
        });
        modelo.Entity<Mao>(e =>
        {
            e.HasIndex(x => new { x.PartidaId, x.Numero }).IsUnique();
            e.HasOne<Partida>().WithMany().HasForeignKey(x => x.PartidaId);
        });
        modelo.Entity<MaoJogador>(e =>
        {
            e.HasKey(x => new { x.MaoId, x.Assento });
            e.HasOne<Mao>().WithMany().HasForeignKey(x => x.MaoId);
        });
        modelo.Entity<Bau>(e =>
        {
            e.HasIndex(x => new { x.MaoId, x.Numero }).IsUnique();
            e.HasOne<Mao>().WithMany().HasForeignKey(x => x.MaoId);
        });
        modelo.Entity<Rodada>(e =>
        {
            e.HasIndex(x => new { x.MaoId, x.Numero }).IsUnique();
            e.HasOne<Mao>().WithMany().HasForeignKey(x => x.MaoId);
        });
        modelo.Entity<Jogada>(e => e.HasOne<Rodada>().WithMany().HasForeignKey(x => x.RodadaId));
        modelo.Entity<MovimentoDeFicha>(e =>
        {
            e.HasOne<Partida>().WithMany().HasForeignKey(x => x.PartidaId);
            e.HasOne<Mao>().WithMany().HasForeignKey(x => x.MaoId).OnDelete(DeleteBehavior.NoAction);
        });

        foreach (var tipo in modelo.Model.GetEntityTypes())
            tipo.SetTableName(tipo.ClrType.Name);
    }
}

/// <summary>Usada só pelas ferramentas `dotnet ef`; não tem conexão real.</summary>
[ExcludeFromCodeCoverage]
public sealed class BestiaDbContextFabrica : IDesignTimeDbContextFactory<BestiaDbContext>
{
    public BestiaDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BestiaDbContext>()
            .UseSqlServer("Server=.;Database=Bestia;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True")
            .Options);
}
