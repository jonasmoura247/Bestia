using Bestia.Aplicacao.Modelos;
using Bestia.Infraestrutura;
using Bestia.Infraestrutura.Repositorios;
using Bestia.Infraestrutura.Seguranca;
using NUnit.Framework;

namespace Bestia.Testes.Integracao;

public class AdaptadoresTestes
{
    [Test]
    public async Task Repositorio_Deve_AdicionarBuscarListarERemover()
    {
        await using var banco = BancoDeTestes.Novo();
        var repositorio = new RepositorioEf(banco);
        var hash = Guid.NewGuid().ToByteArray();
        var jogador = new Jogador { Id = Guid.NewGuid(), EmailHash = hash, CriadoEm = DateTime.UtcNow };

        repositorio.Adicionar(jogador);
        await repositorio.SalvarAsync();
        var achado = await repositorio.BuscarAsync<Jogador>(j => j.EmailHash == hash);
        var lista = await repositorio.ListarAsync<Jogador>(j => j.EmailHash == hash);
        repositorio.Remover(achado!);
        await repositorio.SalvarAsync();

        Assert.That(achado!.Id, Is.EqualTo(jogador.Id));
        Assert.That(lista, Has.Count.EqualTo(1));
        Assert.That(await repositorio.BuscarAsync<Jogador>(j => j.EmailHash == hash), Is.Null);
    }

    [Test]
    public void HashDeSenha_Deve_ConferirSoASenhaCerta()
    {
        var hasher = new HashDeSenhaIdentity();

        var hash = hasher.Gerar("uma-senha-longa");

        Assert.That(hash, Is.Not.EqualTo("uma-senha-longa"));
        Assert.That(hasher.Conferir(hash, "uma-senha-longa"), Is.True);
        Assert.That(hasher.Conferir(hash, "outra-senha"), Is.False);
    }

    [Test]
    public async Task Relogio_Deve_DarAHoraEEsperar()
    {
        var relogio = new RelogioDoSistema();

        await relogio.EsperarAsync(TimeSpan.FromMilliseconds(5));

        Assert.That(relogio.AgoraUtc, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
    }

    private sealed class FabricaDeTeste : Microsoft.EntityFrameworkCore.IDbContextFactory<Bestia.Infraestrutura.Persistencia.BestiaDbContext>
    {
        public Bestia.Infraestrutura.Persistencia.BestiaDbContext CreateDbContext() => BancoDeTestes.Novo();
    }

    [Test]
    public async Task Verificador_Deve_ConfirmarQueOBancoEstaNoAr()
    {
        var verificador = new VerificadorDeBancoEf(new FabricaDeTeste());

        Assert.That(await verificador.EstaDisponivelAsync(), Is.True);
    }

    private sealed class ListaDeLog : Microsoft.Extensions.Logging.ILogger<Bestia.Infraestrutura.Email.EnviadorDeEmailNoLog>
    {
        public List<string> Mensagens { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) => Mensagens.Add(formatter(state, exception));
    }

    [Test]
    public async Task EmailNoLog_Deve_EscreverNoLogEmVezDeEnviar()
    {
        var registro = new ListaDeLog();

        var caixa = new Bestia.Infraestrutura.Email.CaixaDeEmails();

        await new Bestia.Infraestrutura.Email.EnviadorDeEmailNoLog(registro, caixa).EnviarAsync("a@exemplo.com", "Assunto", "https://x/c?t=1");

        Assert.That(registro.Mensagens.Single(), Does.Contain("a@exemplo.com").And.Contain("https://x/c?t=1"));
        Assert.That(caixa.Listar().Single().Corpo, Is.EqualTo("https://x/c?t=1"));
    }

    [Test]
    public void CaixaDeEmails_Deve_GuardarSoOsCinquentaMaisNovos_NaOrdemDoMaisNovo()
    {
        var caixa = new Bestia.Infraestrutura.Email.CaixaDeEmails();

        for (var i = 0; i < 60; i++)
            caixa.Adicionar(new Bestia.Infraestrutura.Email.MensagemDeEmail(DateTime.UtcNow, "a@x.com", $"#{i}", ""));

        var lista = caixa.Listar();
        Assert.That(lista, Has.Count.EqualTo(50));
        Assert.That(lista[0].Assunto, Is.EqualTo("#59"));
        Assert.That(lista[^1].Assunto, Is.EqualTo("#10"));
    }
}
