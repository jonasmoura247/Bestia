using Bestia.Dominio;
using Bestia.Dominio.Jogadores;
using Bestia.Dominio.Partidas;
using NUnit.Framework;

namespace Bestia.Testes.Dominio;

public class PartidaTestes
{
    private static List<Jogador> Jogadores(int quantidade, int fichas = 20) =>
        Enumerable.Range(0, quantidade).Select(i => new Jogador($"J{i}", fichas: fichas)).ToList();

    private static void TodosCorrem(Mao mao)
    {
        while (mao.Fase == FaseDaMao.Decisao)
            mao.Decidir(false);
    }

    [TestCase(2)]
    [TestCase(11)]
    public void Deve_RecusarQuantidadeInvalidaDeJogadores(int quantidade)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Partida(Jogadores(quantidade), new SorteadorSemEmbaralhar()));
    }

    [Test]
    public void Deve_SentarTodosEEscolherDistribuidorEntreEles()
    {
        var jogadores = Jogadores(5);

        var partida = new Partida(jogadores, new SorteadorComSemente(7));

        Assert.That(partida.Assentos, Is.EquivalentTo(jogadores));
        Assert.That(partida.Assentos, Does.Contain(partida.Distribuidor));
        Assert.That(partida.Encerrada, Is.False);
    }

    [Test]
    public void Deve_PassarODistribuidorAoProximoVivo_AoEncerrarAMao()
    {
        var jogadores = Jogadores(4);
        var partida = new Partida(jogadores, new SorteadorSemEmbaralhar());
        Assert.That(partida.Distribuidor, Is.SameAs(jogadores[3]));

        TodosCorrem(partida.IniciarMao());
        partida.EncerrarMao();

        Assert.That(partida.Distribuidor, Is.SameAs(jogadores[0]));
        Assert.That(partida.MaoAtual, Is.Null);
    }

    [Test]
    public void Deve_RecusarIniciarMao_QuandoJaHaUmaEmCurso()
    {
        var partida = new Partida(Jogadores(3), new SorteadorSemEmbaralhar());
        partida.IniciarMao();

        Assert.Throws<InvalidOperationException>(() => partida.IniciarMao());
    }

    [Test]
    public void Deve_RecusarEncerrarMao_QuandoNaoHaMaoConcluida()
    {
        var partida = new Partida(Jogadores(3), new SorteadorSemEmbaralhar());

        Assert.Throws<InvalidOperationException>(() => partida.EncerrarMao());
        partida.IniciarMao();
        Assert.Throws<InvalidOperationException>(() => partida.EncerrarMao());
    }

    [Test]
    public void Deve_EliminarQuemFicaComMenosDeTresFichas_EEncerrarComMenosDeTresJogadores()
    {
        var jogadores = new List<Jogador> { new("J0", fichas: 2), new("J1", fichas: 2), new("J2") };
        var partida = new Partida(jogadores, new SorteadorSemEmbaralhar());

        TodosCorrem(partida.IniciarMao());
        partida.EncerrarMao();

        Assert.That(jogadores[0].MotivoDaSaida, Is.EqualTo(MotivoDaSaida.SemFichas));
        Assert.That(jogadores[2].EstaNaPartida, Is.True);
        Assert.That(partida.Encerrada, Is.True);
        Assert.Throws<InvalidOperationException>(() => partida.IniciarMao());
    }

    [Test]
    public void Deve_EliminarPorInatividade_QuemEstouraOTempoNasTresRodadas()
    {
        var jogadores = Jogadores(3);
        var partida = new Partida(jogadores, new SorteadorSemEmbaralhar());
        var mao = partida.IniciarMao();
        while (mao.Fase == FaseDaMao.Decisao)
            mao.Decidir(true);

        while (mao.Fase == FaseDaMao.Rodadas)
            mao.TempoEsgotadoNaJogada();
        partida.EncerrarMao();

        Assert.That(jogadores.All(j => j.MotivoDaSaida == MotivoDaSaida.Inatividade), Is.True);
        Assert.That(partida.Encerrada, Is.True);
    }

    [Test]
    public void Deve_RetirarQuemDesistiu_SoNoFimDaMao()
    {
        var jogadores = Jogadores(4);
        var partida = new Partida(jogadores, new SorteadorSemEmbaralhar());
        var mao = partida.IniciarMao();

        Assert.That(partida.Desistir(jogadores[1]).Sucesso, Is.True);
        Assert.That(jogadores[1].EstaNaPartida, Is.True);
        TodosCorrem(mao);
        partida.EncerrarMao();

        Assert.That(jogadores[1].MotivoDaSaida, Is.EqualTo(MotivoDaSaida.Desistencia));
        Assert.That(partida.Vivos, Has.Count.EqualTo(3));
        Assert.That(partida.Encerrada, Is.False);
    }

    [Test]
    public void Deve_RetirarNaHora_QuemDesisteEntreMaos()
    {
        var jogadores = Jogadores(4);
        var partida = new Partida(jogadores, new SorteadorSemEmbaralhar());

        var resultado = partida.Desistir(jogadores[1]);

        Assert.That(resultado.Sucesso, Is.True);
        Assert.That(jogadores[1].EstaNaPartida, Is.False);
    }

    [Test]
    public void Deve_ProibirSairAoProximoDistribuidor()
    {
        var jogadores = Jogadores(4);
        var partida = new Partida(jogadores, new SorteadorSemEmbaralhar());

        Assert.That(partida.Desistir(jogadores[3]).ChaveDoErro, Is.EqualTo(ChavesDeErro.ProximoDistribuidorNaoPodeSair));
        partida.IniciarMao();
        Assert.That(partida.Desistir(jogadores[0]).ChaveDoErro, Is.EqualTo(ChavesDeErro.ProximoDistribuidorNaoPodeSair));
    }

    [Test]
    public void Deve_RecusarDesistenciaDeQuemJaSaiu()
    {
        var jogadores = Jogadores(4);
        var partida = new Partida(jogadores, new SorteadorSemEmbaralhar());
        partida.Desistir(jogadores[1]);

        Assert.That(partida.Desistir(jogadores[1]).ChaveDoErro, Is.EqualTo(ChavesDeErro.JogadorForaDaPartida));
    }

    [Test]
    public void Deve_OrdenarRankingPorFichas()
    {
        var jogadores = new List<Jogador> { new("J0", fichas: 5), new("J1", fichas: 30), new("J2", fichas: 12) };
        var partida = new Partida(jogadores, new SorteadorSemEmbaralhar());

        Assert.That(partida.Ranking.Select(j => j.Nome), Is.EqualTo(new[] { "J1", "J2", "J0" }));
    }
}
