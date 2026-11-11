using Bestia.Aplicacao.Modelos;
using Bestia.Aplicacao.Partidas;
using Bestia.Dominio;
using Bestia.Infraestrutura.Persistencia;
using Bestia.Infraestrutura.Repositorios;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Bestia.Testes.Integracao;

public class PartidaNoBancoTestes
{
    private sealed class Fabrica : IDbContextFactory<BestiaDbContext>
    {
        public BestiaDbContext CreateDbContext() => BancoDeTestes.Novo();
    }

    private sealed class Sorteador(int semente) : ISorteador
    {
        private readonly Random _aleatorio = new(semente);

        public int Sortear(int limiteExclusivo) => _aleatorio.Next(limiteExclusivo);
    }

    private sealed class Relogio : Bestia.Aplicacao.Contratos.IRelogio
    {
        private readonly List<(DateTime Limite, TaskCompletionSource Fim)> _esperas = [];

        public DateTime AgoraUtc { get; private set; } = DateTime.UtcNow;

        public Task EsperarAsync(TimeSpan duracao, CancellationToken ct = default)
        {
            var fim = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _esperas.Add((AgoraUtc + duracao, fim));
            return fim.Task;
        }

        public void Avancar(TimeSpan tempo)
        {
            AgoraUtc += tempo;
            foreach (var espera in _esperas.Where(e => e.Limite <= AgoraUtc).ToList())
                espera.Fim.TrySetResult();
        }
    }

    private sealed class Notificador : INotificadorDaMesa
    {
        public Task MesaAtualizadaAsync(Guid contaId, VisaoDaMesa visao) => Task.CompletedTask;

        public Task EmoteAsync(Guid contaId, int assento, int emote) => Task.CompletedTask;
    }

    [Test]
    public async Task Deve_GravarUmaPartidaCompleta_NoSqlServer()
    {
        var contaId = Guid.NewGuid();
        await using (var banco = BancoDeTestes.Novo())
        {
            banco.Add(new Jogador { Id = contaId, ApelidoHash = Guid.NewGuid().ToByteArray(), EmailHash = Guid.NewGuid().ToByteArray(), CriadoEm = DateTime.UtcNow });
            await banco.SaveChangesAsync();
        }
        var relogio = new Relogio();
        var servico = new MesaServico(new UnidadeDeTrabalhoEf(new Fabrica()), relogio, new Sorteador(5), new Notificador());
        var perfil = new PerfilDoJogador(contaId, "Ana", "#e8c05a", "pt-BR", true);
        var codigo = (await servico.CriarSalaAsync(perfil, "Mesa de teste", 4, false)).Valor!;
        await servico.PreencherComBotsAsync(contaId, 3);
        await servico.IniciarPartidaAsync(contaId);

        for (var passo = 0; passo < 5000 && servico.ObterVisao(contaId)!.Fase != "encerrada"; passo++)
        {
            var visao = servico.ObterVisao(contaId)!;
            if (visao.Vez == visao.MeuAssento && visao.Etapa == "decisao")
            {
                await servico.DecidirAsync(contaId, visao.Decisao!.PodeFicar);
                continue;
            }
            relogio.Avancar(TimeSpan.FromSeconds(visao.PrazoFimUtc is null ? 3 : 46));
            await servico.TarefaAgendadaDe(codigo);
        }

        Assert.That(servico.UltimoErroDe(codigo), Is.Null);
        await using var leitura = BancoDeTestes.Novo();
        var sala = await leitura.Set<Sala>().SingleAsync(s => s.Codigo == codigo);
        var partida = await leitura.Set<Partida>().SingleAsync(p => p.SalaId == sala.Id);
        Assert.That(sala.Situacao, Is.EqualTo(SituacaoDaSala.Encerrada));
        Assert.That(partida.Situacao, Is.EqualTo(SituacaoDaPartida.Encerrada));
        var jogadores = await leitura.Set<PartidaJogador>().Where(p => p.PartidaId == partida.Id).ToListAsync();
        Assert.That(jogadores, Has.Count.EqualTo(4));
        Assert.That(jogadores.Count(j => j.JogadorId == contaId), Is.EqualTo(1));
        Assert.That(jogadores.Select(j => j.Colocacao), Is.EquivalentTo(new int?[] { 1, 2, 3, 4 }));
        var maos = await leitura.Set<Mao>().CountAsync(m => m.PartidaId == partida.Id);
        Assert.That(maos, Is.GreaterThan(0));
        Assert.That(await leitura.Set<MovimentoDeFicha>().CountAsync(m => m.PartidaId == partida.Id), Is.GreaterThanOrEqualTo(maos));
    }
}
