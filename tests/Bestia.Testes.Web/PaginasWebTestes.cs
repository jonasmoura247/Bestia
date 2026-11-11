using System.Net;
using System.Text.Json;
using Bestia.Aplicacao.Modelos;
using Bestia.Aplicacao.Contratos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Bestia.Web.Configuracao;
using Bestia.Web.Hubs;
using Bestia.Web.Idioma;
using Bestia.Web.Seguranca;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace Bestia.Testes.Web;

public class PaginasWebTestes
{
    private static async Task<ClienteDeTeste> LogadoAsync(BestiaFactory fabrica, string apelido = "Jonas", string email = "jonas@exemplo.com", bool confirmar = true)
    {
        var cliente = fabrica.NovoCliente();
        await cliente.CadastrarAsync(apelido, email);
        if (confirmar)
        {
            foreach (var conta in fabrica.Repositorio.Todos<Jogador>())
                conta.EmailConfirmadoEm ??= fabrica.Relogio.AgoraUtc;
        }
        await cliente.EntrarAsync(email);
        return cliente;
    }

    [Test]
    public async Task Regras_Deve_ShowTodasAsSecoes_SemLogin_EEmCadaIdioma()
    {
        await using var fabrica = new BestiaFactory();

        foreach (var (idioma, trecho) in new[] { ("pt-BR", "Objetivo"), ("en", "Goal"), ("es", "Objetivo") })
        {
            var pagina = await fabrica.NovoCliente(idioma).TextoAsync("/regras");
            Assert.That(pagina, Does.Contain(trecho));
            Assert.That(pagina, Does.Contain("id=\"regras-sec12\""));
            Assert.That(pagina, Does.Not.Contain("Regras_Sec"));
        }
    }

    [Test]
    public async Task Legal_Deve_MostrarTermosEPrivacidadeComOEncarregado()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();

        var termos = await cliente.TextoAsync("/legal/termos");
        var privacidade = await cliente.TextoAsync("/legal/privacidade");

        Assert.That(termos, Does.Contain("Termos de uso"));
        Assert.That(privacidade, Does.Contain("dpo@exemplo.com"));
    }

    [Test]
    public async Task Perfil_Deve_MostrarEstatisticasEHistorico()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = await LogadoAsync(fabrica);
        var conta = fabrica.Repositorio.Todos<Jogador>().Single();
        var partidaId = Guid.NewGuid();
        fabrica.Repositorio.Adicionar(new Partida
        {
            Id = partidaId,
            SalaId = Guid.NewGuid(),
            IniciadaEm = fabrica.Relogio.AgoraUtc,
            QuantidadeDeJogadores = 4,
            Situacao = SituacaoDaPartida.Encerrada,
        });
        fabrica.Repositorio.Adicionar(new PartidaJogador { PartidaId = partidaId, Assento = 0, JogadorId = conta.Id, FichasFinais = 31, Colocacao = 1 });
        await fabrica.Repositorio.SalvarAsync();

        var pagina = await cliente.TextoAsync("/perfil");

        Assert.That(pagina, Does.Contain("Perfil — Jonas"));
        Assert.That(pagina, Does.Contain("1º de 4"));
        Assert.That(pagina, Does.Contain("31"));
    }

    [Test]
    public async Task Perfil_Deve_AvisarQuandoNaoHaPartidas()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = await LogadoAsync(fabrica);

        Assert.That(await cliente.TextoAsync("/perfil"), Does.Contain("ainda não jogou"));
    }

    [Test]
    public async Task Configuracoes_Deve_AbrirParaQuemEstaLogado()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = await LogadoAsync(fabrica);

        var pagina = await cliente.TextoAsync("/configuracoes");

        Assert.That(pagina, Does.Contain("Reduzir animações"));
        Assert.That(pagina, Does.Contain("id=\"menu-lateral\""));
    }

    [Test]
    public async Task Erro_Deve_MostrarPaginaAmigavel_ParaRotaInexistente()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();

        var resposta = await cliente.GetAsync("/nao-existe");

        Assert.That(resposta.Status(), Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await resposta.CorpoAsync(), Does.Contain("Página não encontrada"));
    }

    [Test]
    public async Task Erro_Deve_EsconderDetalhesTecnicos_EmProducao()
    {
        await using var fabrica = new BestiaFactory { Ambiente = "Production" };
        fabrica.Configuracoes["Bestia:ExigirHttps"] = "false";
        var cliente = fabrica.NovoCliente();

        var resposta = await cliente.GetAsync("/conta/confirmar");

        Assert.That(resposta.Status(), Is.EqualTo(HttpStatusCode.InternalServerError));
        var texto = await resposta.CorpoAsync();
        Assert.That(texto, Does.Contain("Algo deu errado"));
        Assert.That(texto, Does.Not.Contain("NullReference"));
    }

    [Test]
    public async Task Producao_Deve_RedirecionarParaHttps_ENaoPermitirSemChaves()
    {
        await using var fabrica = new BestiaFactory { Ambiente = "Production" };
        fabrica.Configuracoes["Bestia:ExigirHttps"] = "true";
        fabrica.Configuracoes["https_port"] = "443";
        var cliente = fabrica.NovoCliente();

        var resposta = await cliente.GetAsync("/conta/entrar");

        Assert.That(resposta.Status(), Is.EqualTo(HttpStatusCode.RedirectKeepVerb).Or.EqualTo(HttpStatusCode.Redirect));
        Assert.That(resposta.Headers.Location!.Scheme, Is.EqualTo("https"));
    }

    [Test]
    public async Task Deve_EnviarCabecalhosDeSeguranca()
    {
        await using var fabrica = new BestiaFactory();
        var resposta = await fabrica.NovoCliente().GetAsync("/conta/entrar");

        Assert.That(resposta.Headers.GetValues("Content-Security-Policy").Single(), Does.Contain("script-src 'self'"));
        Assert.That(resposta.Headers.GetValues("X-Content-Type-Options").Single(), Is.EqualTo("nosniff"));
        Assert.That(resposta.Headers.GetValues("X-Frame-Options").Single(), Is.EqualTo("DENY"));
        Assert.That(resposta.Headers.GetValues("Referrer-Policy").Single(), Is.EqualTo("no-referrer"));
    }

    [Test]
    public async Task Recursos_Deve_EntregarOsTextosDoNavegadorNoIdioma()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();

        var json = JsonDocument.Parse(await cliente.TextoAsync("/recursos/en.json"));

        Assert.That(json.RootElement.GetProperty("Partida_Ficar").GetString(), Is.EqualTo("Stay"));
        Assert.That(json.RootElement.TryGetProperty("Conta_Senha", out _), Is.False);
    }

    [Test]
    public async Task ChavesDoDataProtection_Devem_IrParaAPastaConfigurada()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "bestia-dp-" + Guid.NewGuid().ToString("N"));
        await using (var fabrica = new BestiaFactory())
        {
            fabrica.Configuracoes["DataProtection:Pasta"] = pasta;
            var cliente = fabrica.NovoCliente();
            await cliente.TextoAsync("/conta/entrar");
        }

        Assert.That(Directory.GetFiles(pasta, "key-*.xml"), Is.Not.Empty);
        Directory.Delete(pasta, recursive: true);
    }

    [Test]
    public async Task Saude_Deve_RefletirOEstadoDoBanco()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();

        var ok = await cliente.GetAsync("/saude");
        fabrica.Banco.Disponivel = false;
        var fora = await cliente.GetAsync("/saude");

        Assert.That(ok.Status(), Is.EqualTo(HttpStatusCode.OK));
        Assert.That(fora.Status(), Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    // ---------- salas ----------

    [Test]
    public async Task Salas_Deve_CriarSala_EntrarPorCodigo_EListar()
    {
        await using var fabrica = new BestiaFactory();
        var ana = await LogadoAsync(fabrica, "Ana", "ana@exemplo.com");
        var bia = await LogadoAsync(fabrica, "Bia", "bia@exemplo.com");

        var criar = await ana.PostAsync("/salas/criar", new() { ["Nome"] = "Mesa do canto", ["Maximo"] = "5" }, "/salas");
        var codigo = criar.Local()[7..];
        var pagina = await ana.GetAsync(criar.Local());
        var lista = JsonDocument.Parse(await bia.TextoAsync("/salas/lista")).RootElement;
        var entrar = await bia.PostAsync("/salas/entrar", new() { ["Codigo"] = codigo.ToLowerInvariant() }, "/salas");
        var lobbyDaBia = await bia.TextoAsync("/salas");

        Assert.That(criar.Local(), Does.StartWith("/salas/"));
        Assert.That(await pagina.CorpoAsync(), Does.Contain("id=\"mesa-app\""));
        Assert.That(lista[0].GetProperty("nome").GetString(), Is.EqualTo("Mesa do canto"));
        Assert.That(entrar.Local(), Is.EqualTo("/salas/" + codigo));
        Assert.That(lobbyDaBia, Does.Contain("Voltar para a minha mesa"));
    }

    [Test]
    public async Task Salas_Deve_ExigirEmailConfirmado_EMostrarErros()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = await LogadoAsync(fabrica, confirmar: false);

        var lobby = await cliente.TextoAsync("/salas");
        var criar = await cliente.PostAsync("/salas/criar", new() { ["Nome"] = "Mesa", ["Maximo"] = "5" }, "/salas");
        var erro = await cliente.TextoAsync(criar.Local());
        var inexistente = await cliente.GetAsync("/salas/ZZZZZZ");

        Assert.That(lobby, Does.Contain("Confirme seu e-mail para poder"));
        Assert.That(criar.Local(), Does.Contain("erro=Erro_EmailNaoConfirmado"));
        Assert.That(erro, Does.Contain("Confirme seu e-mail para entrar em uma sala"));
        Assert.That(inexistente.Local(), Does.Contain("erro="));
    }

    [Test]
    public async Task Salas_Deve_ListarSalasAbertasNoLobby_ComOuSemPartida()
    {
        await using var fabrica = new BestiaFactory();
        var ana = await LogadoAsync(fabrica, "Ana", "ana@exemplo.com");
        var bia = await LogadoAsync(fabrica, "Bia", "bia@exemplo.com");
        var criar = await ana.PostAsync("/salas/criar", new() { ["Nome"] = "Mesa aberta", ["Maximo"] = "4" }, "/salas");
        var codigo = criar.Local()[7..];
        await bia.PostAsync("/salas/entrar", new() { ["Codigo"] = codigo }, "/salas");
        var caio = await LogadoAsync(fabrica, "Caio", "caio@exemplo.com");

        var comVaga = await caio.TextoAsync("/salas");
        var servico = fabrica.Services.GetService(typeof(Bestia.Aplicacao.Partidas.MesaServico)) as Bestia.Aplicacao.Partidas.MesaServico;
        var contaAna = fabrica.Repositorio.Todos<SalaJogador>().First().JogadorId;
        await servico!.PreencherComBotsAsync(contaAna, 1);
        await servico.IniciarPartidaAsync(contaAna);
        var emPartida = await caio.TextoAsync("/salas");

        Assert.That(comVaga, Does.Contain("Mesa aberta"));
        Assert.That(comVaga, Does.Contain("2 de 4 jogadores"));
        Assert.That(emPartida, Does.Contain("Em partida"));
    }

    // ---------- unidades ----------

    [Test]
    public void Idioma_Deve_SeguirAOrdemPerfilCookieCabecalhoPadrao()
    {
        Assert.That(ResolvedorDeIdioma.Resolver("en", "es", "pt"), Is.EqualTo("en"));
        Assert.That(ResolvedorDeIdioma.Resolver(null, "es", "en"), Is.EqualTo("es"));
        Assert.That(ResolvedorDeIdioma.Resolver(null, null, "fr, en-GB;q=0.8"), Is.EqualTo("en"));
        Assert.That(ResolvedorDeIdioma.Resolver("", " ", null), Is.EqualTo("pt-BR"));
        Assert.That(ResolvedorDeIdioma.Resolver("klingon", null, "pt-PT"), Is.EqualTo("pt-BR"));
    }

    [Test]
    public void Criptografia_Deve_ExigirChavesEmProducao_ELiberarFallbackEmDesenvolvimento()
    {
        var vazio = new ConfigurationBuilder().Build();
        var producao = new FakeAmbiente("Production");
        var desenvolvimento = new FakeAmbiente("Development");

        Assert.Throws<InvalidOperationException>(() => ServicosDoBestia.CriarCriptografia(vazio, producao));
        var dev = ServicosDoBestia.CriarCriptografia(vazio, desenvolvimento);
        Assert.That(dev.Decifrar(dev.Cifrar("segredo")), Is.EqualTo("segredo"));
    }

    [Test]
    public void Email_Deve_IrParaOLog_EmDesenvolvimentoSemServidor_ESmtpNosDemais()
    {
        IEnviadorDeEmail Resolver(string ambiente, Dictionary<string, string?> config)
        {
            var configuracao = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
            var servicos = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            servicos.AddLogging();
            servicos.AdicionarBestia(configuracao, new FakeAmbiente(ambiente));
            return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<IEnviadorDeEmail>(servicos.BuildServiceProvider());
        }

        var desenvolvimento = Resolver("Development", []);
        var producao = Resolver("Production", new() { ["Email:Servidor"] = "smtp.exemplo.com" });
        var desenvolvimentoComServidor = Resolver("Development", new() { ["Email:Servidor"] = "smtp.exemplo.com" });

        Assert.That(desenvolvimento, Is.InstanceOf<Bestia.Infraestrutura.Email.EnviadorDeEmailNoLog>());
        Assert.That(producao, Is.InstanceOf<Bestia.Infraestrutura.Email.EnviadorDeEmailSmtp>());
        Assert.That(desenvolvimentoComServidor, Is.InstanceOf<Bestia.Infraestrutura.Email.EnviadorDeEmailSmtp>());
    }

    [Test]
    public async Task DevEmails_Deve_ListarOsEmailsComLinksClicaveis_SoEmDesenvolvimento()
    {
        await using var desenvolvimento = new BestiaFactory();
        var caixa = (Bestia.Infraestrutura.Email.CaixaDeEmails)desenvolvimento.Services.GetService(typeof(Bestia.Infraestrutura.Email.CaixaDeEmails))!;
        var cliente = desenvolvimento.NovoCliente();
        var vazia = await cliente.TextoAsync("/dev/emails");
        caixa.Adicionar(new Bestia.Infraestrutura.Email.MensagemDeEmail(DateTime.UtcNow, "a@exemplo.com", "Confirme <já>", "Use https://x/conta/confirmar?token=abc agora"));

        var cheia = await (await cliente.GetAsync("/dev/emails")).Content.ReadAsStringAsync();
        await using var producao = new BestiaFactory { Ambiente = "Production" };
        producao.Configuracoes["Bestia:ExigirHttps"] = "false";
        var negada = await producao.NovoCliente().GetAsync("/dev/emails");

        var pedidoDeTunel = new HttpRequestMessage(HttpMethod.Get, "/dev/emails");
        pedidoDeTunel.Headers.Add("X-Forwarded-For", "203.0.113.9");
        var viaTunel = await cliente.EnviarAsync(pedidoDeTunel);

        var hostPublico = new HttpRequestMessage(HttpMethod.Get, "/dev/emails");
        hostPublico.Headers.Host = "948363797b2cfb.lhr.life";
        var viaHostPublico = await cliente.EnviarAsync(hostPublico);
        string[] hostsDaRede = ["192.168.18.7", "10.0.0.5", "172.20.1.1", "127.0.0.1"];
        var permitidos = new List<HttpStatusCode>();
        foreach (var hostLocal in hostsDaRede)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Get, "/dev/emails");
            pedido.Headers.Host = hostLocal;
            permitidos.Add((await cliente.EnviarAsync(pedido)).Status());
        }
        var hostIpPublico = new HttpRequestMessage(HttpMethod.Get, "/dev/emails");
        hostIpPublico.Headers.Host = "8.8.8.8";
        var ipPublico = await cliente.EnviarAsync(hostIpPublico);
        var hostIpv6 = new HttpRequestMessage(HttpMethod.Get, "/dev/emails");
        hostIpv6.Headers.Host = "172.40.1.1";
        var fora172 = await cliente.EnviarAsync(hostIpv6);

        var hostIpv6Publico = new HttpRequestMessage(HttpMethod.Get, "/dev/emails");
        hostIpv6Publico.Headers.Host = "[2001:db8::1]";
        var ipv6 = await cliente.EnviarAsync(hostIpv6Publico);

        Assert.That(ipv6.Status(), Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(viaTunel.Status(), Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(viaHostPublico.Status(), Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(ipPublico.Status(), Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(fora172.Status(), Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(permitidos, Has.All.EqualTo(HttpStatusCode.OK));
        Assert.That(vazia, Does.Contain("Nenhum e-mail ainda"));
        Assert.That(cheia, Does.Contain("<a href=\"https://x/conta/confirmar?token=abc\">"));
        Assert.That(cheia, Does.Contain("Confirme &lt;j&#225;&gt;"));
        Assert.That(negada.Status(), Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task EmailNoLog_Deve_RegistrarSemEnviar()
    {
        var registro = new ListaDeLog<Bestia.Infraestrutura.Email.EnviadorDeEmailNoLog>();
        var enviador = new Bestia.Infraestrutura.Email.EnviadorDeEmailNoLog(registro, new Bestia.Infraestrutura.Email.CaixaDeEmails());

        await enviador.EnviarAsync("a@exemplo.com", "Assunto", "Corpo https://x");

        Assert.That(registro.Mensagens.Single(), Does.Contain("a@exemplo.com").And.Contain("https://x"));
    }

    private sealed class ListaDeLog<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<string> Mensagens { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) => Mensagens.Add(formatter(state, exception));
    }

    [Test]
    public void Limitador_Deve_PermitirVinteMensagensPorSegundo()
    {
        var relogio = new RelogioDeTeste();
        var limitador = new LimitadorDeMensagens(relogio);

        var primeiroSegundo = Enumerable.Range(0, 25).Select(_ => limitador.Permitir("c1")).ToList();
        relogio.Avancar(TimeSpan.FromSeconds(1));
        var segundo = limitador.Permitir("c1");
        var outraConexao = limitador.Permitir("c2");
        limitador.Esquecer("c1");

        Assert.That(primeiroSegundo.Count(p => p), Is.EqualTo(20));
        Assert.That(segundo, Is.True);
        Assert.That(outraConexao, Is.True);
    }

    [Test]
    public void Limitador_Deve_UsarORelogioDoSistema_PorPadrao()
    {
        Assert.That(new LimitadorDeMensagens().Permitir("x"), Is.True);
    }

    [Test]
    public void Conexoes_Deve_DevolverAConexaoAnterior()
    {
        var conexoes = new ControleDeConexoes();
        var conta = Guid.NewGuid();

        var primeira = conexoes.Registrar(conta, "a");
        var segunda = conexoes.Registrar(conta, "b");
        conexoes.Remover(conta, "a");
        conexoes.Remover(conta, "b");
        var terceira = conexoes.Registrar(conta, "c");

        Assert.That(primeira, Is.Null);
        Assert.That(segunda, Is.EqualTo("a"));
        Assert.That(terceira, Is.Null);
    }

    [Test]
    public async Task Sessao_Deve_SerValidaSoComClaimsCompletasECarimboAtual()
    {
        var repositorio = new Bestia.Testes.Aplicacao.RepositorioEmMemoria();
        var criptografia = new Bestia.Testes.Aplicacao.CriptografiaFalsa();
        var contas = new Bestia.Aplicacao.Contas.ContaServico(
            repositorio, criptografia, new Bestia.Testes.Aplicacao.HashDeSenhaFalso(),
            new Bestia.Testes.Aplicacao.RelogioFalso(), new Bestia.Testes.Aplicacao.EnviadorDeEmailFalso(),
            new Bestia.Recursos.ProvedorDeTextos());
        var conta = new Jogador { Id = Guid.NewGuid(), CarimboDeSeguranca = Guid.NewGuid(), ApelidoCifrado = criptografia.Cifrar("Ana") };
        repositorio.Adicionar(conta);
        await repositorio.SalvarAsync();
        var sessao = new Bestia.Aplicacao.Contas.SessaoDeConta(conta.Id, "Ana", "pt-BR", true, conta.CarimboDeSeguranca, "#fff");

        var valida = Bestia.Web.Seguranca.Sessao.Criar(sessao);
        var outraConta = Bestia.Web.Seguranca.Sessao.Criar(sessao with { Carimbo = Guid.NewGuid() });
        var semCarimbo = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, conta.Id.ToString())]));
        var semConta = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());

        Assert.That(await Bestia.Web.Seguranca.Sessao.EhValidaAsync(contas, valida), Is.True);
        Assert.That(await Bestia.Web.Seguranca.Sessao.EhValidaAsync(contas, outraConta), Is.False);
        Assert.That(await Bestia.Web.Seguranca.Sessao.EhValidaAsync(contas, semCarimbo), Is.False);
        Assert.That(await Bestia.Web.Seguranca.Sessao.EhValidaAsync(contas, semConta), Is.False);
        Assert.That(await Bestia.Web.Seguranca.Sessao.EhValidaAsync(contas, null), Is.False);
        Assert.That(valida.ContaId(), Is.EqualTo(conta.Id));
    }

    private sealed class FakeAmbiente(string nome) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nome;
        public string ApplicationName { get; set; } = "Bestia";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class RelogioDeTeste : TimeProvider
    {
        private DateTimeOffset _agora = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan tempo) => _agora += tempo;
    }
}
