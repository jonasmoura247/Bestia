using System.Security.Cryptography;
using System.Text;
using Bestia.Infraestrutura.Seguranca;
using NUnit.Framework;

namespace Bestia.Testes.Integracao;

public class CriptografiaTestes
{
    private static readonly byte[] ChaveUm = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private static readonly byte[] ChaveDois = Enumerable.Range(101, 32).Select(i => (byte)i).ToArray();
    private static readonly byte[] ChaveDeIndice = Enumerable.Range(201, 32).Select(i => (byte)(i % 256)).ToArray();

    private static CriptografiaAesGcm Criar(byte idAtual = 1) =>
        new(new Dictionary<byte, byte[]> { [1] = ChaveUm, [2] = ChaveDois }.Where(p => p.Key <= idAtual).ToDictionary(), idAtual, ChaveDeIndice);

    [TestCase("jonas@exemplo.com")]
    [TestCase("")]
    [TestCase("Ação — çãõ 🂡")]
    public void Deve_DecifrarOQueCifrou(string texto)
    {
        var criptografia = Criar();

        Assert.That(criptografia.Decifrar(criptografia.Cifrar(texto)), Is.EqualTo(texto));
    }

    [Test]
    public void Deve_GerarNonceNovo_ENaoGuardarOTextoEmClaro()
    {
        var criptografia = Criar();

        var primeiro = criptografia.Cifrar("segredo");
        var segundo = criptografia.Cifrar("segredo");

        Assert.That(primeiro, Is.Not.EqualTo(segundo));
        Assert.That(Encoding.UTF8.GetString(primeiro), Does.Not.Contain("segredo"));
    }

    [Test]
    public void Deve_DetectarAdulteracao()
    {
        var criptografia = Criar();
        var valor = criptografia.Cifrar("segredo");
        valor[^1] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() => criptografia.Decifrar(valor));
    }

    [Test]
    public void Deve_RecusarValorCurto_VersaoDesconhecida_OuChaveDesconhecida()
    {
        var criptografia = Criar();
        var valor = criptografia.Cifrar("segredo");
        var versaoErrada = (byte[])valor.Clone();
        versaoErrada[0] = 9;
        var chaveErrada = (byte[])valor.Clone();
        chaveErrada[1] = 7;

        Assert.Throws<CryptographicException>(() => criptografia.Decifrar([1, 1, 0]));
        Assert.Throws<CryptographicException>(() => criptografia.Decifrar(versaoErrada));
        Assert.Throws<CryptographicException>(() => criptografia.Decifrar(chaveErrada));
    }

    [Test]
    public void Deve_ContinuarDecifrandoValoresAntigos_AposGirarAChave()
    {
        var antiga = Criar(idAtual: 1);
        var nova = Criar(idAtual: 2);
        var valorAntigo = antiga.Cifrar("segredo");

        var valorNovo = nova.Cifrar("segredo");

        Assert.That(nova.Decifrar(valorAntigo), Is.EqualTo("segredo"));
        Assert.That(valorNovo[1], Is.EqualTo(2));
        Assert.That(valorAntigo[1], Is.EqualTo(1));
    }

    [Test]
    public void Deve_GerarIndiceCegoEstavel_ENormalizado()
    {
        var criptografia = Criar();
        var outra = Criar(idAtual: 2);

        Assert.That(criptografia.IndiceCego("  Jonas@Exemplo.com "), Is.EqualTo(criptografia.IndiceCego("jonas@exemplo.com")));
        Assert.That(criptografia.IndiceCego("a@x.com"), Is.Not.EqualTo(criptografia.IndiceCego("b@x.com")));
        Assert.That(outra.IndiceCego("a@x.com"), Is.EqualTo(criptografia.IndiceCego("a@x.com")), "girar a chave de cifra não muda o índice");
    }

    [Test]
    public void Deve_GerarHashDeTokenDiferenteDoIndiceCego()
    {
        var criptografia = Criar();

        Assert.That(criptografia.HashDeToken("abc"), Is.EqualTo(criptografia.HashDeToken("abc")));
        Assert.That(criptografia.HashDeToken("abc"), Is.Not.EqualTo(criptografia.IndiceCego("abc")));
    }
}
