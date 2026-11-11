using Bestia.Dominio;
using Bestia.Dominio.Cartas;
using Bestia.Dominio.Jogadores;
using Bestia.Dominio.Partidas;
using NUnit.Framework;
using static Bestia.Testes.Dominio.Cenario;

namespace Bestia.Testes.Dominio;

public class MaoTestes
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
    public void Deve_CobrarAposta_ESomarMontantePendente_AoComecar()
    {
        var mao = Nova(_a, _b, _c, montantePendente: 6);

        Assert.That(_a.Fichas, Is.EqualTo(17));
        Assert.That(mao.Montante, Is.EqualTo(9));
        Assert.That(mao.ValorDaRodada, Is.EqualTo(3));
        Assert.That(mao.CartaDaMania, Is.EqualTo(ManiaPadrao));
        Assert.That(mao.CartasDe(_b), Is.EqualTo(Fortes));
    }

    [Test]
    public void Deve_DecidirComODistribuidorPorUltimo()
    {
        var mao = Nova(_a, _b, _c);
        var ordem = new List<Jogador?>();

        while (mao.Fase == FaseDaMao.Decisao)
        {
            ordem.Add(mao.JogadorDaVez);
            mao.Decidir(false);
        }

        Assert.That(ordem, Is.EqualTo(new[] { _b, _c, _a }));
        Assert.That(mao.JogadorDaVez, Is.Null);
    }

    [Test]
    public void Deve_DarMontanteAoDistribuidor_QuandoTodosCorrem()
    {
        var mao = Nova(_a, _b, _c);

        mao.Decidir(false);
        mao.Decidir(false);
        mao.Decidir(false);

        Assert.That(mao.Desfecho, Is.EqualTo(DesfechoDaMao.TodosCorreram));
        Assert.That(mao.Fase, Is.EqualTo(FaseDaMao.Concluida));
        Assert.That(_a.Fichas, Is.EqualTo(20));
        Assert.That(mao.MontantePendente, Is.EqualTo(0));
    }

    [Test]
    public void Deve_DarMontanteInteiro_AoUnicoQueFicou_SemJogar()
    {
        var mao = Nova(_a, _b, _c);

        mao.Decidir(true);
        mao.Decidir(false);
        mao.Decidir(false);

        Assert.That(mao.Desfecho, Is.EqualTo(DesfechoDaMao.UnicoQueFicou));
        Assert.That(_b.Fichas, Is.EqualTo(23));
    }

    [Test]
    public void Deve_PagarUmTercoPorRodada_ECobrarReposicaoDeQuemPerdeuTudo()
    {
        var mao = Nova(_a, _b, _c);
        mao.Decidir(true);
        mao.Decidir(true);
        mao.Decidir(false);

        Assert.That(mao.Fase, Is.EqualTo(FaseDaMao.Rodadas));
        foreach (var indice in new[] { 0, 1, 2 })
        {
            Assert.That(mao.JogadorDaVez, Is.SameAs(_b));
            Assert.That(mao.JogarCarta(Fortes[indice]).Sucesso, Is.True);
            Assert.That(mao.JogarCarta(Medias[indice]).Sucesso, Is.True);
        }

        Assert.That(mao.Desfecho, Is.EqualTo(DesfechoDaMao.Disputada));
        Assert.That(_b.Fichas, Is.EqualTo(23));
        Assert.That(_c.Fichas, Is.EqualTo(17));
        Assert.That(mao.MontantePendente, Is.EqualTo(3));
        Assert.That(_a.Fichas + _b.Fichas + _c.Fichas + mao.MontantePendente, Is.EqualTo(60));
    }

    [Test]
    public void Deve_DeixarQuemVenceuARodadaAbrirAProxima()
    {
        Carta[] maoC = [C(Valor.Quatro, Naipe.Espadas), C(Valor.Dama, Naipe.Ouros), C(Valor.Valete, Naipe.Ouros)];
        var mao = Nova(_a, _b, _c, maoC: maoC);
        mao.Decidir(true);
        mao.Decidir(true);
        mao.Decidir(false);

        mao.JogarCarta(C(Valor.As, Naipe.Paus));
        mao.JogarCarta(C(Valor.Quatro, Naipe.Espadas));

        Assert.That(_c.Fichas, Is.EqualTo(21));
        Assert.That(mao.JogadorDaVez, Is.SameAs(_c));
        Assert.That(mao.JogadasDaRodada, Is.Empty);
    }

    [Test]
    public void Deve_ExigirSeguirONaipe_QuandoTemACarta()
    {
        Carta[] maoC = [C(Valor.Rei, Naipe.Paus), C(Valor.Dama, Naipe.Ouros), C(Valor.Valete, Naipe.Ouros)];
        var mao = Nova(_a, _b, _c, maoC: maoC);
        mao.Decidir(true);
        mao.Decidir(true);
        mao.Decidir(false);
        mao.JogarCarta(C(Valor.As, Naipe.Paus));

        Assert.That(mao.JogarCarta(C(Valor.Dama, Naipe.Ouros)).ChaveDoErro, Is.EqualTo(ChavesDeErro.DeveSeguirONaipe));
        Assert.That(mao.JogarCarta(C(Valor.Quatro, Naipe.Copas)).ChaveDoErro, Is.EqualTo(ChavesDeErro.CartaNaoEhSua));
        Assert.That(mao.JogarCarta(C(Valor.Rei, Naipe.Paus)).Sucesso, Is.True);
    }

    [Test]
    public void Deve_RecusarAcoesForaDeHora()
    {
        var mao = Nova(_a, _b, _c);

        Assert.That(mao.JogarCarta(Fortes[0]).ChaveDoErro, Is.EqualTo(ChavesDeErro.AcaoForaDeHora));
        Assert.That(mao.TempoEsgotadoNaJogada().ChaveDoErro, Is.EqualTo(ChavesDeErro.AcaoForaDeHora));

        mao.Decidir(true);
        mao.Decidir(true);
        mao.Decidir(false);

        Assert.That(mao.Decidir(true).ChaveDoErro, Is.EqualTo(ChavesDeErro.AcaoForaDeHora));
        Assert.That(mao.PegarBau(0).ChaveDoErro, Is.EqualTo(ChavesDeErro.AcaoForaDeHora));
        Assert.That(mao.TempoEsgotadoNaDecisao().ChaveDoErro, Is.EqualTo(ChavesDeErro.AcaoForaDeHora));
    }

    [Test]
    public void Deve_PermitirSoCorrer_QuemNaoCobreOMontante()
    {
        var b = new Jogador("B", fichas: 8);
        var mao = Nova(_a, b, _c, montantePendente: 6);

        Assert.That(mao.PodeFicar(b), Is.False);
        Assert.That(mao.Decidir(true).ChaveDoErro, Is.EqualTo(ChavesDeErro.FichasInsuficientes));
        Assert.That(mao.PegarBau(0).ChaveDoErro, Is.EqualTo(ChavesDeErro.FichasInsuficientes));
        Assert.That(mao.Decidir(false).Sucesso, Is.True);
    }

    [Test]
    public void Deve_TrocarAMao_EObrigarAFicar_QuandoPegaBau()
    {
        var mao = Nova(_a, _b, _c);

        Assert.That(mao.PegarBau(0).Sucesso, Is.True);

        Assert.That(mao.CartasDe(_b), Is.EqualTo(Bau1));
        Assert.That(mao.BausLivres(), Is.EqualTo(new[] { 1 }));
        Assert.That(mao.Decidir(false).ChaveDoErro, Is.EqualTo(ChavesDeErro.ObrigadoAFicar));
        Assert.That(mao.Decidir(true).Sucesso, Is.True);
        Assert.That(mao.JogadorDaVez, Is.SameAs(_c));
    }

    [Test]
    public void Deve_CobrarTaxa_JogarComOUltimoBau_EEncerrarADecisao_QuandoMesmoJogadorPegaOsDois()
    {
        var mao = Nova(_a, _b, _c);

        mao.PegarBau(0);
        mao.PegarBau(1);

        Assert.That(_b.Fichas, Is.EqualTo(17));
        Assert.That(mao.Montante, Is.EqualTo(6));
        Assert.That(mao.CartasDe(_b), Is.EqualTo(Bau2));
        Assert.That(mao.JogadorDaVez, Is.SameAs(_c));
    }

    [Test]
    public void Deve_NaoCobrarTaxa_EEncerrarADecisao_QuandoOutroJogadorPegaOSegundoBau()
    {
        var mao = Nova(_a, _b, _c);
        mao.PegarBau(0);
        mao.Decidir(true);

        Assert.That(mao.PegarBau(0).ChaveDoErro, Is.EqualTo(ChavesDeErro.BauJaPego));
        mao.PegarBau(1);

        Assert.That(_c.Fichas, Is.EqualTo(20));
        Assert.That(mao.Montante, Is.EqualTo(3));
        Assert.That(mao.JogadorDaVez, Is.SameAs(_a));
        mao.Decidir(false);
        Assert.That(mao.Fase, Is.EqualTo(FaseDaMao.Rodadas));
    }

    [Test]
    public void Deve_RecusarBauInexistente()
    {
        var mao = Nova(_a, _b, _c);

        Assert.That(mao.PegarBau(-1).ChaveDoErro, Is.EqualTo(ChavesDeErro.BauInexistente));
        Assert.That(mao.PegarBau(2).ChaveDoErro, Is.EqualTo(ChavesDeErro.BauInexistente));
    }

    [Test]
    public void Deve_RecusarSegundoBau_QuandoQuemPegaNaoCobreONovoMontante()
    {
        var b = new Jogador("B", fichas: 8);
        var mao = Nova(_a, b, _c);
        mao.PegarBau(0);

        Assert.That(mao.PegarBau(1).ChaveDoErro, Is.EqualTo(ChavesDeErro.TaxaDoBauSemFichas));
        Assert.That(b.Fichas, Is.EqualTo(8));
    }

    [Test]
    public void Deve_RecusarSegundoBau_QuandoQuemJaFicouNaoCobreONovoMontante()
    {
        var b = new Jogador("B", fichas: 5);
        var mao = Nova(_a, b, _c);
        mao.Decidir(true);
        mao.PegarBau(0);

        Assert.That(mao.PegarBau(1).ChaveDoErro, Is.EqualTo(ChavesDeErro.TaxaDoBauSemFichas));
    }

    [Test]
    public void Deve_CorrerSozinho_QuandoTempoDaDecisaoEstouraSemBau()
    {
        var mao = Nova(_a, _b, _c);

        mao.TempoEsgotadoNaDecisao();

        Assert.That(mao.JogadorDaVez, Is.SameAs(_c));
        mao.Decidir(true);
        mao.Decidir(false);
        Assert.That(mao.Desfecho, Is.EqualTo(DesfechoDaMao.UnicoQueFicou));
        Assert.That(_c.Fichas, Is.EqualTo(23));
    }

    [Test]
    public void Deve_FicarComAMaoAtual_QuandoTempoDaDecisaoEstouraComBau()
    {
        var mao = Nova(_a, _b, _c);
        mao.PegarBau(0);

        mao.TempoEsgotadoNaDecisao();
        mao.Decidir(false);
        mao.Decidir(false);

        Assert.That(mao.Desfecho, Is.EqualTo(DesfechoDaMao.UnicoQueFicou));
        Assert.That(_b.Fichas, Is.EqualTo(23));
    }

    [Test]
    public void Deve_JogarACartaMaisFracaVirada_EContarAusencia_QuandoTempoDaJogadaEstoura()
    {
        Carta[] maoB = [C(Valor.As, Naipe.Espadas), C(Valor.Cinco, Naipe.Paus), C(Valor.Quatro, Naipe.Ouros)];
        var mao = Nova(_a, _b, _c, maoB: maoB);
        mao.Decidir(true);
        mao.Decidir(true);
        mao.Decidir(false);

        mao.TempoEsgotadoNaJogada();

        var jogada = mao.JogadasDaRodada.Single();
        Assert.That(jogada.Carta, Is.EqualTo(C(Valor.Quatro, Naipe.Ouros)));
        Assert.That(jogada.FoiAutomatica, Is.True);
        Assert.That(mao.Ausencias(_b), Is.EqualTo(1));
        Assert.That(mao.Ausencias(_c), Is.EqualTo(0));
    }

    [Test]
    public void Deve_SeguirONaipe_NaJogadaAutomatica()
    {
        Carta[] maoC = [C(Valor.Rei, Naipe.Paus), C(Valor.Dama, Naipe.Ouros), C(Valor.Valete, Naipe.Ouros)];
        var mao = Nova(_a, _b, _c, maoC: maoC);
        mao.Decidir(true);
        mao.Decidir(true);
        mao.Decidir(false);
        mao.JogarCarta(C(Valor.As, Naipe.Paus));

        mao.TempoEsgotadoNaJogada();

        Assert.That(mao.JogadasDaRodada, Is.Empty);
        Assert.That(_b.Fichas, Is.EqualTo(21));
    }
}
