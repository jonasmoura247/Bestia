using System.Diagnostics;
using Bestia.Aplicacao.Contratos;
using Bestia.Aplicacao.Partidas;
using Bestia.Dominio;
using NUnit.Framework;

namespace Bestia.Testes.Aplicacao;

/// <summary>Teste de carga em memória: muitas mesas ao mesmo tempo, com relógio simulado.</summary>
public class CargaTestes
{
    private sealed class NotificadorNulo : INotificadorDaMesa
    {
        public long Visoes;

        public Task MesaAtualizadaAsync(Guid contaId, VisaoDaMesa visao)
        {
            Interlocked.Increment(ref Visoes);
            return Task.CompletedTask;
        }

        public Task EmoteAsync(Guid contaId, int assento, int emote) => Task.CompletedTask;
    }

    /// <summary>Serializa o acesso ao repositório de teste, como um banco faria.</summary>
    private sealed class UnidadeDeTrabalhoSerial(IRepositorio repositorio) : IUnidadeDeTrabalho
    {
        private readonly SemaphoreSlim _trava = new(1, 1);

        public async Task ExecutarAsync(Func<IRepositorio, Task> trabalho, CancellationToken ct = default)
        {
            await _trava.WaitAsync(ct);
            try
            {
                await trabalho(repositorio);
            }
            finally
            {
                _trava.Release();
            }
        }
    }

    [Test]
    public async Task Deve_SustentarCinquentaMesasSimultaneas_ATerminarTodasAsPartidas()
    {
        const int mesas = 50;
        var repositorio = new RepositorioEmMemoria();
        var relogio = new RelogioFalso();
        var notificador = new NotificadorNulo();
        var servico = new MesaServico(new UnidadeDeTrabalhoSerial(repositorio), relogio, new SorteadorCriptografico(), notificador);
        var jogadores = Enumerable.Range(0, mesas).Select(i => new PerfilDoJogador(Guid.NewGuid(), $"J{i}", "#fff", "pt-BR", true)).ToList();
        var codigos = new List<string>();
        foreach (var jogador in jogadores)
        {
            codigos.Add((await servico.CriarSalaAsync(jogador, "Mesa de carga", 6, false)).Valor!);
            await servico.PreencherComBotsAsync(jogador.ContaId, 5);
            await servico.IniciarPartidaAsync(jogador.ContaId);
        }

        var relogioDeParede = Stopwatch.StartNew();
        var rodadas = 0;
        while (jogadores.Any(j => servico.ObterVisao(j.ContaId)!.Fase != "encerrada"))
        {
            Assert.That(++rodadas, Is.LessThan(20000), "as partidas não terminaram");
            var ativas = jogadores.Count(j => servico.ObterVisao(j.ContaId)!.Fase != "encerrada");
            Assert.That(relogioDeParede.Elapsed, Is.LessThan(TimeSpan.FromSeconds(100)),
                $"travou: {ativas} mesas ativas, {relogio.Esperas} esperas, {notificador.Visoes} visões, {rodadas} passos, erros: {string.Join("; ", codigos.Select(servico.UltimoErroDe).Where(e => e is not null).Select(e => e!.Message).Distinct())}");
            relogio.Avancar(TimeSpan.FromSeconds(46));
            // As mesas reagem em paralelo; considera "ocioso" quando nenhuma visão nova sai por um instante.
            long antes;
            do
            {
                antes = Interlocked.Read(ref notificador.Visoes);
                await Task.Delay(15);
            }
            while (Interlocked.Read(ref notificador.Visoes) != antes);
        }
        relogioDeParede.Stop();

        TestContext.Out.WriteLine(
            $"{mesas} mesas de 6 jogadores terminaram em {relogioDeParede.Elapsed.TotalSeconds:F1} s " +
            $"({rodadas} passos do relógio, {notificador.Visoes} visões enviadas, {repositorio.Todos<Bestia.Aplicacao.Modelos.Mao>().Count()} mãos gravadas).");
        Assert.That(codigos.Select(servico.UltimoErroDe), Has.All.Null);
        Assert.That(repositorio.Todos<Bestia.Aplicacao.Modelos.Partida>().Count(p => p.Situacao == Bestia.Aplicacao.Modelos.SituacaoDaPartida.Encerrada), Is.EqualTo(mesas));
        Assert.That(relogioDeParede.Elapsed, Is.LessThan(TimeSpan.FromSeconds(100)));
    }
}
