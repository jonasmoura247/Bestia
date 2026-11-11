using System.Reflection;
using System.Text.RegularExpressions;
using Bestia.Dominio;
using Bestia.Recursos;
using NUnit.Framework;

namespace Bestia.Testes.Aplicacao;

public class TextosTestes
{
    private static readonly string[] Idiomas3 = ["pt-BR", "en", "es"];

    private static IEnumerable<string> Constantes(Type tipo) =>
        tipo.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!);

    [Test]
    public void Deve_TerAsConstantesIguaisAsChavesDoArquivoMestre()
    {
        Assert.That(Constantes(typeof(Textos)), Is.EquivalentTo(ProvedorDeTextos.Mestre.Keys));
    }

    [Test]
    public void Deve_TerOsTresIdiomasPreenchidos_ComOsMesmosMarcadores()
    {
        foreach (var (chave, traducoes) in ProvedorDeTextos.Mestre)
        {
            Assert.That(traducoes.Keys, Is.EquivalentTo(Idiomas3), chave);
            Assert.That(traducoes.Values, Has.All.Not.Empty, chave);
            var marcadores = traducoes.Values
                .Select(t => string.Join(",", Regex.Matches(t, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x)))
                .Distinct();
            Assert.That(marcadores, Has.Exactly(1).Items, $"marcadores diferentes em {chave}");
        }
    }

    [Test]
    public void Deve_TerTextoParaToda_ChaveDeErroDoDominio()
    {
        Assert.That(ProvedorDeTextos.Mestre.Keys, Is.SupersetOf(Constantes(typeof(ChavesDeErro))));
    }

    [Test]
    public void Deve_UsarAChaveNaoTraduzida_NaoOTexto_NoPadraoDeNome()
    {
        Assert.That(ProvedorDeTextos.Mestre.Keys, Has.All.Match(@"^[A-Z][A-Za-z0-9]*_[A-Za-z0-9_]+$"));
    }

    [Test]
    public void Deve_TraduzirEPreencherMarcadores()
    {
        var textos = new ProvedorDeTextos();

        Assert.That(textos.Obter(Textos.Menu_Sair, "en"), Is.EqualTo("Sign out"));
        Assert.That(textos.Obter(Textos.Menu_Sair, "zz"), Is.EqualTo("Sair"));
        Assert.That(textos.Obter(Textos.Email_Confirmacao_Corpo, "es", "https://x"), Does.Contain("https://x"));
        Assert.That(textos.Obter("Chave_Que_Nao_Existe", "en"), Is.EqualTo("Chave_Que_Nao_Existe"));
    }

    [TestCase("pt-BR", "pt-BR")]
    [TestCase("EN", "en")]
    [TestCase("es", "es")]
    [TestCase("fr", "pt-BR")]
    [TestCase(null, "pt-BR")]
    public void Idiomas_Deve_Normalizar(string? entrada, string esperado)
    {
        Assert.That(Idiomas.Normalizar(entrada), Is.EqualTo(esperado));
    }
}
