using Bestia.Aplicacao.Modelos;
using Bestia.Aplicacao.Perfil;
using NUnit.Framework;

namespace Bestia.Testes.Aplicacao;

public class PerfilServicoTestes
{
    [Test]
    public async Task Deve_SomarPartidasConcluidas_VitoriasEFichas_EListarOHistoricoRecente()
    {
        var repositorio = new RepositorioEmMemoria();
        var conta = Guid.NewGuid();
        var agora = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

        void Partida(int diasAtras, int colocacao, int fichas, SituacaoDaPartida situacao, Guid? dono = null)
        {
            var id = Guid.NewGuid();
            repositorio.Adicionar(new Partida
            {
                Id = id,
                SalaId = Guid.NewGuid(),
                IniciadaEm = agora.AddDays(-diasAtras),
                QuantidadeDeJogadores = 4,
                Situacao = situacao,
            });
            repositorio.Adicionar(new PartidaJogador
            {
                PartidaId = id,
                Assento = 1,
                JogadorId = dono ?? conta,
                Colocacao = colocacao,
                FichasFinais = fichas,
            });
        }

        Partida(3, 1, 40, SituacaoDaPartida.Encerrada);
        Partida(2, 3, 5, SituacaoDaPartida.Encerrada);
        Partida(1, 0, 20, SituacaoDaPartida.Interrompida);
        Partida(1, 1, 99, SituacaoDaPartida.Encerrada, dono: Guid.NewGuid());
        await repositorio.SalvarAsync();

        var perfil = await new PerfilServico(repositorio).ObterAsync(conta);

        Assert.That(perfil.Partidas, Is.EqualTo(2));
        Assert.That(perfil.Vitorias, Is.EqualTo(1));
        Assert.That(perfil.FichasFinaisSomadas, Is.EqualTo(45));
        Assert.That(perfil.Recentes.Select(r => r.Interrompida), Is.EqualTo(new[] { true, false, false }));
        Assert.That(perfil.Recentes[1].Colocacao, Is.EqualTo(3));
    }

    [Test]
    public async Task Deve_DevolverZeros_SemPartidas()
    {
        var perfil = await new PerfilServico(new RepositorioEmMemoria()).ObterAsync(Guid.NewGuid());

        Assert.That(perfil, Is.EqualTo(new EstatisticasDoPerfil(0, 0, 0, [])).Using<EstatisticasDoPerfil>(
            (a, b) => a.Partidas == b.Partidas && a.Vitorias == b.Vitorias && a.Recentes.Count == 0));
    }
}
