using Bestia.Dominio.Jogadores;
using Bestia.Dominio.Partidas;
using NUnit.Framework;
using static Bestia.Testes.Dominio.Cenario;

namespace Bestia.Testes.Dominio;

public class MaoHistoricoTestes
{
    private Jogador _a = null!;
    private Jogador _b = null!;
    private Jogador _c = null!;

    [SetUp]
    public void Preparar()
    {
        _a = new Jogador("A");
        _b = new Jogador("B");
        _c = new Jogador("C");
    }

    [Test]
    public void Deve_RegistrarMovimentosERodadas_DeUmaMaoDisputada()
    {
        var mao = Nova(_a, _b, _c);
        mao.Decidir(true);
        mao.Decidir(true);
        mao.Decidir(false);
        for (var i = 0; i < 3; i++)
        {
            mao.JogarCarta(Fortes[i]);
            mao.JogarCarta(Medias[i]);
        }

        Assert.That(mao.Participantes, Is.EqualTo(new[] { _a, _b, _c }));
        Assert.That(mao.Movimentos.Select(m => (m.Jogador, m.Tipo, m.Valor)), Is.EqualTo(new[]
        {
            (_a, TipoDeMovimento.ApostaDoDistribuidor, -3),
            (_b, TipoDeMovimento.PremioDaRodada, 1),
            (_b, TipoDeMovimento.PremioDaRodada, 1),
            (_b, TipoDeMovimento.PremioDaRodada, 1),
            (_c, TipoDeMovimento.ReposicaoDoMontante, -3),
        }));
        Assert.That(mao.Rodadas.Select(r => r.Numero), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(mao.Rodadas[0].Abridor, Is.SameAs(_b));
        Assert.That(mao.Rodadas[0].Vencedor, Is.SameAs(_b));
        Assert.That(mao.Rodadas[0].Jogadas, Has.Count.EqualTo(2));
        Assert.That(mao.Vitorias(_b), Is.EqualTo(3));
        Assert.That(mao.Vitorias(_c), Is.EqualTo(0));
        Assert.That(mao.DecisaoDe(_a), Is.EqualTo(DecisaoNaMao.Correu));
        Assert.That(mao.Abridor, Is.Null);
        Assert.That(mao.ProximoJogador, Is.Null);
    }

    [Test]
    public void Deve_RegistrarMovimentoDoDistribuidor_QuandoTodosCorrem()
    {
        var mao = Nova(_a, _b, _c);

        mao.Decidir(false);
        mao.Decidir(false);
        mao.Decidir(false);

        Assert.That(mao.Movimentos.Last(), Is.EqualTo(new Movimento(_a, TipoDeMovimento.MontanteDoDistribuidor, 3)));
    }

    [Test]
    public void Deve_RegistrarMovimentoDoUnicoQueFicou()
    {
        var mao = Nova(_a, _b, _c);

        mao.Decidir(true);
        mao.Decidir(false);
        mao.Decidir(false);

        Assert.That(mao.Movimentos.Last(), Is.EqualTo(new Movimento(_b, TipoDeMovimento.PremioDoUnicoQueFicou, 3)));
        Assert.That(mao.DecisaoDe(_b), Is.EqualTo(DecisaoNaMao.Ficou));
    }

    [Test]
    public void Deve_RegistrarDonoETaxaDosBaus()
    {
        var mao = Nova(_a, _b, _c);

        mao.PegarBau(0);
        mao.PegarBau(1);

        Assert.That(mao.DonoDoBau(0), Is.SameAs(_b));
        Assert.That(mao.DonoDoBau(1), Is.SameAs(_b));
        Assert.That(mao.TaxaDoBau(0), Is.EqualTo(0));
        Assert.That(mao.TaxaDoBau(1), Is.EqualTo(3));
        Assert.That(mao.BausPegosPor(_b), Is.EqualTo(2));
        Assert.That(mao.DecisaoDe(_b), Is.EqualTo(DecisaoNaMao.Ficou));
        Assert.That(mao.Movimentos, Does.Contain(new Movimento(_b, TipoDeMovimento.TaxaDoBau, -3)));
    }

    [Test]
    public void Deve_MarcarAusente_QuandoOTempoDaDecisaoEstouraSemBau()
    {
        var mao = Nova(_a, _b, _c);

        mao.TempoEsgotadoNaDecisao();
        mao.PegarBau(0);
        mao.TempoEsgotadoNaDecisao();

        Assert.That(mao.DecisaoDe(_b), Is.EqualTo(DecisaoNaMao.Ausente));
        Assert.That(mao.DecisaoDe(_c), Is.EqualTo(DecisaoNaMao.Ficou));
    }

    [Test]
    public void Deve_IndicarQuemAbreEQuemJogaDepois()
    {
        var mao = Nova(_a, _b, _c);
        Assert.That(mao.JogadorDaVez, Is.SameAs(_b));
        Assert.That(mao.ProximoJogador, Is.SameAs(_c));
        Assert.That(mao.Abridor, Is.Null);
        Assert.That(mao.Rodada, Is.EqualTo(0));

        mao.Decidir(true);
        mao.Decidir(true);
        Assert.That(mao.JogadorDaVez, Is.SameAs(_a));
        Assert.That(mao.ProximoJogador, Is.Null);
        mao.Decidir(false);

        Assert.That(mao.Rodada, Is.EqualTo(1));
        Assert.That(mao.Abridor, Is.SameAs(_b));
        Assert.That(mao.ProximoJogador, Is.SameAs(_c));
        mao.JogarCarta(Fortes[0]);
        Assert.That(mao.ProximoJogador, Is.Null);
    }
    [Test]
    public void Deve_InformarSeOJogadorDaVezPodePegarOBau()
    {
        var mao = Nova(_a, _b, _c);
        Assert.That(mao.PodePegarBau(0), Is.True);
        Assert.That(mao.PodePegarBau(1), Is.True);
        Assert.That(mao.PodePegarBau(-1), Is.False);
        Assert.That(mao.PodePegarBau(2), Is.False);

        mao.PegarBau(0);
        mao.Decidir(true);
        Assert.That(mao.PodePegarBau(0), Is.False);
        Assert.That(mao.PodePegarBau(1), Is.True);

        mao.Decidir(false);
        mao.Decidir(false);
        Assert.That(mao.PodePegarBau(1), Is.False);
    }

    [Test]
    public void Deve_NegarBau_QuandoNaoCobreOMontanteOuATaxaDoSegundo()
    {
        var semFichas = new Jogador("B", fichas: 8);
        var semMontante = Nova(_a, semFichas, _c, montantePendente: 6);
        Assert.That(semMontante.PodePegarBau(0), Is.False);

        var semTaxa = Nova(new Jogador("A"), new Jogador("B", fichas: 8), new Jogador("C"));
        semTaxa.PegarBau(0);
        Assert.That(semTaxa.PodePegarBau(1), Is.False);
    }

    [Test]
    public void Deve_InformarQuemJaDecidiu()
    {
        var mao = Nova(_a, _b, _c);
        Assert.That(mao.JaDecidiu(_b), Is.False);

        mao.Decidir(false);

        Assert.That(mao.JaDecidiu(_b), Is.True);
        Assert.That(mao.JaDecidiu(_c), Is.False);
    }
}
