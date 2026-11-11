using Bestia.Dominio;
using NUnit.Framework;

namespace Bestia.Testes.Dominio;

public class SorteioTestes
{
    [Test]
    public void SorteadorCriptografico_Deve_FicarNoIntervalo()
    {
        var sorteador = new SorteadorCriptografico();

        var sorteados = Enumerable.Range(0, 200).Select(_ => sorteador.Sortear(10)).ToList();

        Assert.That(sorteados, Is.All.InRange(0, 9));
    }

    [Test]
    public void Embaralhar_Deve_ManterOsMesmosItens()
    {
        var itens = Enumerable.Range(0, 20).ToList();

        itens.Embaralhar(new SorteadorComSemente(3));

        Assert.That(itens, Is.EquivalentTo(Enumerable.Range(0, 20)));
        Assert.That(itens, Is.Not.EqualTo(Enumerable.Range(0, 20)));
    }
}
