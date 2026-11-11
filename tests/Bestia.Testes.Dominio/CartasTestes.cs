using Bestia.Dominio.Cartas;
using NUnit.Framework;

namespace Bestia.Testes.Dominio;

public class CartasTestes
{
    [Test]
    public void Baralho_Deve_TerQuarentaEOitoCartasDistintas()
    {
        var baralho = Baralho.Criar();

        Assert.That(baralho, Has.Count.EqualTo(48));
        Assert.That(baralho.Distinct().Count(), Is.EqualTo(48));
    }

    [Test]
    public void Forca_Deve_SeguirAOrdemAs_Tres_Dois_Rei_Dama_Valete_Nove_Ao_Quatro()
    {
        Valor[] doMaisForteAoMaisFraco =
        [
            Valor.As, Valor.Tres, Valor.Dois, Valor.Rei, Valor.Dama, Valor.Valete,
            Valor.Nove, Valor.Oito, Valor.Sete, Valor.Seis, Valor.Cinco, Valor.Quatro,
        ];

        Assert.That(doMaisForteAoMaisFraco, Is.Ordered.Descending);
    }

    [Test]
    public void Rodada_Deve_DarVitoriaAoAs_QuandoJogamAs_Tres_Dois_DoMesmoNaipe()
    {
        var jogadas = new[]
        {
            new Carta(Naipe.Paus, Valor.Tres),
            new Carta(Naipe.Paus, Valor.As),
            new Carta(Naipe.Paus, Valor.Dois),
        };

        Assert.That(ComparadorDeCartas.IndiceDaVencedora(jogadas, Naipe.Ouros), Is.EqualTo(1));
    }

    [Test]
    public void Rodada_Deve_DarVitoriaAMania_MesmoContraCartaMaisAltaDoNaipeAberto()
    {
        var jogadas = new[]
        {
            new Carta(Naipe.Paus, Valor.As),
            new Carta(Naipe.Ouros, Valor.Quatro),
        };

        Assert.That(ComparadorDeCartas.IndiceDaVencedora(jogadas, Naipe.Ouros), Is.EqualTo(1));
    }

    [Test]
    public void Rodada_Deve_IgnorarCartaDeOutroNaipe_QuandoNaoHaMania()
    {
        var jogadas = new[]
        {
            new Carta(Naipe.Paus, Valor.Sete),
            new Carta(Naipe.Copas, Valor.As),
        };

        Assert.That(ComparadorDeCartas.IndiceDaVencedora(jogadas, Naipe.Ouros), Is.EqualTo(0));
    }

    [Test]
    public void Rodada_Deve_EscolherAMaisForte_QuandoHaDuasManias()
    {
        var jogadas = new[]
        {
            new Carta(Naipe.Paus, Valor.As),
            new Carta(Naipe.Ouros, Valor.Cinco),
            new Carta(Naipe.Ouros, Valor.Rei),
        };

        Assert.That(ComparadorDeCartas.IndiceDaVencedora(jogadas, Naipe.Ouros), Is.EqualTo(2));
    }
}
