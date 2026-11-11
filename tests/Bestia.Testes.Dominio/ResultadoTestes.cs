using Bestia.Dominio;
using NUnit.Framework;

namespace Bestia.Testes.Dominio;

public class ResultadoTestes
{
    [Test]
    public void Deve_CarregarOValor_NoSucesso()
    {
        var resultado = Resultado<int>.Ok(7);

        Assert.That(resultado.Sucesso, Is.True);
        Assert.That(resultado.Valor, Is.EqualTo(7));
        Assert.That(resultado.ChaveDoErro, Is.Null);
    }

    [Test]
    public void Deve_CarregarAChaveDoErro_NaFalha()
    {
        var resultado = Resultado<int>.Falha(ChavesDeErro.TokenInvalido);

        Assert.That(resultado.Sucesso, Is.False);
        Assert.That(resultado.Valor, Is.EqualTo(0));
        Assert.That(resultado.ChaveDoErro, Is.EqualTo(ChavesDeErro.TokenInvalido));
    }
}
