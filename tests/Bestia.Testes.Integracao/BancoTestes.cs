using System.Text;
using Bestia.Dominio.Cartas;
using DesfechoDaMao = Bestia.Dominio.Partidas.DesfechoDaMao;
using DecisaoNaMao = Bestia.Dominio.Partidas.DecisaoNaMao;
using TipoDeMovimento = Bestia.Dominio.Partidas.TipoDeMovimento;
using Bestia.Infraestrutura.Persistencia;
using Bestia.Aplicacao.Modelos;
using Bestia.Infraestrutura.Seguranca;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Bestia.Testes.Integracao;

public class BancoTestes
{
    private static readonly byte[] Chave = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

    private static CriptografiaAesGcm Criptografia() =>
        new(new Dictionary<byte, byte[]> { [1] = Chave }, 1, Chave.Reverse().ToArray());

    private static byte[] Hash() => Guid.NewGuid().ToByteArray();

    private static async Task<Jogador> NovoJogadorAsync(BestiaDbContext banco, byte[]? emailHash = null)
    {
        var jogador = new Jogador { Id = Guid.NewGuid(), EmailHash = emailHash ?? Hash(), ApelidoHash = Hash(), CriadoEm = DateTime.UtcNow };
        banco.Add(jogador);
        await banco.SaveChangesAsync();
        return jogador;
    }

    private static async Task<(Jogador Jogador, Sala Sala, Partida Partida)> SemearPartidaAsync(BestiaDbContext banco)
    {
        var jogador = await NovoJogadorAsync(banco);
        var sala = new Sala
        {
            Id = Guid.NewGuid(),
            Nome = "Mesa",
            Codigo = Guid.NewGuid().ToString("N")[..6],
            CriadaPorJogadorId = jogador.Id,
            MaximoDeJogadores = 6,
            CriadaEm = DateTime.UtcNow,
        };
        var partida = new Partida
        {
            Id = Guid.NewGuid(),
            SalaId = sala.Id,
            IniciadaEm = DateTime.UtcNow,
            QuantidadeDeJogadores = 3,
        };
        banco.AddRange(sala, partida);
        await banco.SaveChangesAsync();
        return (jogador, sala, partida);
    }

    [Test]
    public async Task Deve_CriarAsDezesseteTabelas()
    {
        await using var banco = BancoDeTestes.Novo();

        var tabelas = await banco.Database
            .SqlQuery<string>($"SELECT TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME <> '__EFMigrationsHistory'")
            .ToListAsync();

        var esperadas = banco.Model.GetEntityTypes().Select(t => t.GetTableName()).ToList();
        Assert.That(esperadas, Has.Count.EqualTo(17));
        Assert.That(tabelas, Is.SupersetOf(esperadas));
    }

    [Test]
    public async Task Deve_GuardarDadoPessoalIlegivelNoBanco()
    {
        var criptografia = Criptografia();
        var email = "jonas@exemplo.com";
        var id = Guid.NewGuid();
        await using (var gravacao = BancoDeTestes.Novo())
        {
            gravacao.Add(new Jogador
            {
                Id = id,
                CriadoEm = DateTime.UtcNow,
                ApelidoHash = Hash(),
                EmailCifrado = criptografia.Cifrar(email),
                EmailHash = criptografia.IndiceCego(email),
            });
            await gravacao.SaveChangesAsync();
        }

        await using var leitura = BancoDeTestes.Novo();
        var bruto = await leitura.Database
            .SqlQuery<byte[]>($"SELECT EmailCifrado AS Value FROM Jogador WHERE Id = {id}")
            .SingleAsync();
        var achado = await leitura.Set<Jogador>().SingleAsync(j => j.EmailHash == criptografia.IndiceCego("  JONAS@exemplo.com"));

        Assert.That(Encoding.UTF8.GetString(bruto), Does.Not.Contain("jonas"));
        Assert.That(criptografia.Decifrar(achado.EmailCifrado!), Is.EqualTo(email));
    }

    [Test]
    public async Task Deve_RecusarEmailRepetido_MasAceitarVariasContasAnonimizadas()
    {
        await using var banco = BancoDeTestes.Novo();
        var hash = Hash();
        await NovoJogadorAsync(banco, hash);

        Assert.CatchAsync<DbUpdateException>(async () =>
        {
            await using var outro = BancoDeTestes.Novo();
            await NovoJogadorAsync(outro, hash);
        });

        banco.AddRange(
            new Jogador { Id = Guid.NewGuid(), CriadoEm = DateTime.UtcNow, RemovidoEm = DateTime.UtcNow },
            new Jogador { Id = Guid.NewGuid(), CriadoEm = DateTime.UtcNow, RemovidoEm = DateTime.UtcNow });
        Assert.DoesNotThrowAsync(() => banco.SaveChangesAsync());
    }

    [Test]
    public async Task Deve_RecusarLoginExternoERepetidoECodigoDeSalaRepetido()
    {
        await using var banco = BancoDeTestes.Novo();
        var (jogador, sala, _) = await SemearPartidaAsync(banco);
        var externo = Hash();
        banco.Add(new LoginExterno { Id = Guid.NewGuid(), JogadorId = jogador.Id, Provedor = "Google", IdExternoHash = externo, CriadoEm = DateTime.UtcNow });
        await banco.SaveChangesAsync();

        await using var outro = BancoDeTestes.Novo();
        outro.Add(new LoginExterno { Id = Guid.NewGuid(), JogadorId = jogador.Id, Provedor = "Google", IdExternoHash = externo, CriadoEm = DateTime.UtcNow });
        Assert.CatchAsync<DbUpdateException>(() => outro.SaveChangesAsync());

        await using var terceiro = BancoDeTestes.Novo();
        terceiro.Add(new Sala { Id = Guid.NewGuid(), Nome = "Outra", Codigo = sala.Codigo, CriadaPorJogadorId = jogador.Id, MaximoDeJogadores = 4, CriadaEm = DateTime.UtcNow });
        Assert.CatchAsync<DbUpdateException>(() => terceiro.SaveChangesAsync());
    }

    [Test]
    public async Task Deve_RecusarFichasNegativas()
    {
        await using var banco = BancoDeTestes.Novo();
        var (jogador, _, partida) = await SemearPartidaAsync(banco);
        banco.Add(new PartidaJogador { PartidaId = partida.Id, Assento = 0, JogadorId = jogador.Id, FichasIniciais = 20, FichasFinais = -1 });

        Assert.CatchAsync<DbUpdateException>(() => banco.SaveChangesAsync());
    }

    [Test]
    public async Task Deve_DetectarDuasAlteracoesSimultaneasNoJogador()
    {
        await using var inicial = BancoDeTestes.Novo();
        var jogador = await NovoJogadorAsync(inicial);
        await using var primeiro = BancoDeTestes.Novo();
        await using var segundo = BancoDeTestes.Novo();
        var a = await primeiro.Set<Jogador>().SingleAsync(j => j.Id == jogador.Id);
        var b = await segundo.Set<Jogador>().SingleAsync(j => j.Id == jogador.Id);

        a.FalhasDeLogin = 1;
        await primeiro.SaveChangesAsync();
        b.FalhasDeLogin = 2;

        Assert.CatchAsync<DbUpdateConcurrencyException>(() => segundo.SaveChangesAsync());
    }

    [Test]
    public async Task Deve_GravarUmaMaoCompletaEmUmaTransacao()
    {
        await using var banco = BancoDeTestes.Novo();
        var (_, _, partida) = await SemearPartidaAsync(banco);
        var mao = new Mao
        {
            Id = Guid.NewGuid(),
            PartidaId = partida.Id,
            Numero = 1,
            AssentoDoDistribuidor = 2,
            Mania = Naipe.Espadas,
            Montante = 3,
            Desfecho = DesfechoDaMao.Disputada,
        };
        var rodada = new Rodada { Id = Guid.NewGuid(), MaoId = mao.Id, Numero = 1, AbridorAssento = 0, VencedorAssento = 1, Valor = 1 };

        await using (var transacao = await banco.Database.BeginTransactionAsync())
        {
            banco.Add(mao);
            banco.AddRange(
                new MaoJogador { MaoId = mao.Id, Assento = 0, Decisao = DecisaoNaMao.Ficou, RodadasVencidas = 0 },
                new MaoJogador { MaoId = mao.Id, Assento = 1, Decisao = DecisaoNaMao.Ficou, RodadasVencidas = 3 },
                new Bau { Id = Guid.NewGuid(), MaoId = mao.Id, Numero = 1, PegoPorAssento = 0 },
                new Bau { Id = Guid.NewGuid(), MaoId = mao.Id, Numero = 2 },
                rodada,
                new Jogada { Id = Guid.NewGuid(), RodadaId = rodada.Id, Assento = 0, Ordem = 1, Naipe = Naipe.Paus, Valor = Valor.As },
                new MovimentoDeFicha { Id = Guid.NewGuid(), PartidaId = partida.Id, MaoId = mao.Id, Assento = 2, Tipo = TipoDeMovimento.ApostaDoDistribuidor, Valor = -3, OcorreuEm = DateTime.UtcNow });
            await banco.SaveChangesAsync();
            await transacao.CommitAsync();
        }

        await using var leitura = BancoDeTestes.Novo();
        var gravada = await leitura.Set<Mao>().SingleAsync(m => m.Id == mao.Id);
        Assert.That(gravada.Mania, Is.EqualTo(Naipe.Espadas));
        Assert.That(await leitura.Set<Bau>().CountAsync(b => b.MaoId == mao.Id), Is.EqualTo(2));
        Assert.That(await leitura.Set<Jogada>().CountAsync(j => j.RodadaId == rodada.Id), Is.EqualTo(1));

        leitura.Add(new Mao { Id = Guid.NewGuid(), PartidaId = partida.Id, Numero = 1 });
        Assert.CatchAsync<DbUpdateException>(() => leitura.SaveChangesAsync());
    }
}
