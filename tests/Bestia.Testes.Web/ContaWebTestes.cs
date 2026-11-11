using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bestia.Aplicacao.Modelos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Bestia.Testes.Web;

/// <summary>Cria, só nos testes, uma rota que simula o retorno do Google no cookie externo.</summary>
internal sealed class FiltroDeLoginExterno : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> proximo) => app =>
    {
        app.Map("/teste/externo", ramo => ramo.Run(async http =>
        {
            var q = http.Request.Query;
            var claims = new List<Claim>();
            void Adicionar(string parametro, string tipo)
            {
                if (q.ContainsKey(parametro))
                    claims.Add(new Claim(tipo, q[parametro].ToString()));
            }
            Adicionar("id", ClaimTypes.NameIdentifier);
            Adicionar("email", ClaimTypes.Email);
            Adicionar("nome", ClaimTypes.Name);
            Adicionar("verificado", "email_verified");
            await http.SignInAsync("Externo", new ClaimsPrincipal(new ClaimsIdentity(claims, "Externo")));
            http.Response.StatusCode = 204;
        }));
        proximo(app);
    };
}

internal sealed class FabricaComGoogleFalso : BestiaFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s => s.AddTransient<IStartupFilter, FiltroDeLoginExterno>());
    }
}

public class ContaWebTestes
{
    private static string CaminhoDoLink(string corpo)
    {
        var url = Regex.Match(corpo, @"https?://\S+").Value.TrimEnd('.', ' ');
        return new Uri(url).PathAndQuery;
    }

    [Test]
    public async Task Deve_LevarOInicioParaEntrar_QuandoAnonimo_EParaSalas_QuandoLogado()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();
        Assert.That((await cliente.GetAsync("/")).Local(), Is.EqualTo("/conta/entrar"));

        await cliente.CadastrarAsync("Jonas", "jonas@exemplo.com");
        await cliente.EntrarAsync("jonas@exemplo.com");

        Assert.That((await cliente.GetAsync("/")).Local(), Is.EqualTo("/salas"));
    }

    [Test]
    public async Task Deve_CadastrarConfirmarEEntrar()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();

        var cadastro = await cliente.CadastrarAsync("Jonas", "jonas@exemplo.com");
        var corpo = fabrica.Email.Enviados.Single().Corpo;
        var confirmacao = await cliente.TextoAsync(CaminhoDoLink(corpo));
        var entrada = await cliente.EntrarAsync("jonas@exemplo.com");
        var lobby = await cliente.TextoAsync("/salas");

        Assert.That(cadastro.Status(), Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(cadastro.Local(), Does.Contain("aviso=cadastro"));
        Assert.That(corpo, Does.Contain("/conta/confirmar?token="));
        Assert.That(confirmacao, Does.Contain("E-mail confirmado"));
        Assert.That(entrada.Local(), Is.EqualTo("/salas"));
        Assert.That(cliente.TemCookie("bestia.sessao"), Is.True);
        Assert.That(lobby, Does.Contain("Mesas abertas"));
        Assert.That(lobby, Does.Contain("Jonas"));
        Assert.That(fabrica.Repositorio.Todos<Jogador>().Single().EmailConfirmadoEm, Is.Not.Null);
    }

    [Test]
    public async Task Deve_MostrarAvisoDeCadastro_ENegarCadastroInvalido()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();

        var invalido = await cliente.PostAsync("/conta/criar", new()
        {
            ["Apelido"] = "Jonas",
            ["Email"] = "jonas@exemplo.com",
            ["Senha"] = "curta",
        }, "/conta/criar");
        var texto = await invalido.CorpoAsync();
        await cliente.CadastrarAsync("Jonas", "jonas@exemplo.com");
        var aviso = await cliente.TextoAsync("/conta/entrar?aviso=cadastro");

        Assert.That(invalido.Status(), Is.EqualTo(HttpStatusCode.OK));
        Assert.That(texto, Does.Contain("É preciso aceitar"));
        Assert.That(aviso, Does.Contain("Conta criada!"));
    }

    [Test]
    public async Task Deve_ExigirTokenAntiFalsificacao()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();

        var resposta = await cliente.PostSemTokenAsync("/conta/entrar", new() { ["Email"] = "a@b.com", ["Senha"] = "x" });

        Assert.That(resposta.Status(), Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Deve_DarMesmaMensagem_ParaSenhaErrada()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();
        await cliente.CadastrarAsync("Jonas", "jonas@exemplo.com");

        var errada = await cliente.EntrarAsync("jonas@exemplo.com", "senha-errada-aqui");
        var desconhecido = await cliente.EntrarAsync("ninguem@exemplo.com");

        Assert.That(await errada.CorpoAsync(), Does.Contain("E-mail ou senha inválidos"));
        Assert.That(await desconhecido.CorpoAsync(), Does.Contain("E-mail ou senha inválidos"));
    }

    [Test]
    public async Task Deve_RespeitarRetornoUrlLocal_EIgnorarExterno()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();
        await cliente.CadastrarAsync("Jonas", "jonas@exemplo.com");

        var local = await cliente.PostAsync("/conta/entrar",
            new() { ["Email"] = "jonas@exemplo.com", ["Senha"] = "uma-senha-bem-longa", ["RetornoUrl"] = "/perfil" });
        var externo = await cliente.PostAsync("/conta/entrar",
            new() { ["Email"] = "jonas@exemplo.com", ["Senha"] = "uma-senha-bem-longa", ["RetornoUrl"] = "https://mal.com" });

        Assert.That(local.Local(), Is.EqualTo("/perfil"));
        Assert.That(externo.Local(), Is.EqualTo("/salas"));
    }

    [Test]
    public async Task Deve_SairEPedirLoginDeNovo()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();
        await cliente.CadastrarAsync("Jonas", "jonas@exemplo.com");
        await cliente.EntrarAsync("jonas@exemplo.com");

        var saida = await cliente.PostAsync("/conta/sair", [], "/salas");
        var protegida = await cliente.GetAsync("/salas");

        Assert.That(saida.Local(), Is.EqualTo("/conta/entrar"));
        Assert.That(protegida.Local(), Does.StartWith("/conta/entrar"));
    }

    [Test]
    public async Task Deve_TraduzirConformeOCabecalhoAcceptLanguage()
    {
        await using var fabrica = new BestiaFactory();

        var ingles = await fabrica.NovoCliente("en-US,en;q=0.9").TextoAsync("/conta/entrar");
        var espanhol = await fabrica.NovoCliente("es-MX").TextoAsync("/conta/entrar");
        var padrao = await fabrica.NovoCliente("fr-FR").TextoAsync("/conta/entrar");

        Assert.That(ingles, Does.Contain("Enter the tavern"));
        Assert.That(espanhol, Does.Contain("Entrar en la taberna"));
        Assert.That(padrao, Does.Contain("Entrar na taverna"));
    }

    [Test]
    public async Task Deve_GuardarOIdiomaNoCookieENoPerfil()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();

        var anonimo = await cliente.PostAsync("/conta/idioma", new() { ["idioma"] = "en", ["retornoUrl"] = "/conta/entrar" });
        var pagina = await cliente.TextoAsync("/conta/entrar");
        await cliente.CadastrarAsync("Jonas", "jonas@exemplo.com");
        await cliente.EntrarAsync("jonas@exemplo.com");
        await cliente.PostAsync("/conta/idioma", new() { ["idioma"] = "es" }, "/salas");
        var logado = await cliente.TextoAsync("/salas");

        Assert.That(anonimo.Headers.GetValues("Set-Cookie").Single(), Does.Contain("bestia.idioma=en"));
        Assert.That(pagina, Does.Contain("Enter the tavern"));
        Assert.That(logado, Does.Contain("Mesas abiertas"));
        Assert.That(fabrica.Repositorio.Todos<Jogador>().Single().Idioma, Is.EqualTo("es"));
    }

    [Test]
    public async Task Deve_RecuperarASenha_EInvalidarAsSessoesAbertas()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();
        await cliente.CadastrarAsync("Jonas", "jonas@exemplo.com");
        var outroAparelho = fabrica.NovoCliente();
        await outroAparelho.EntrarAsync("jonas@exemplo.com");
        Assert.That((await outroAparelho.GetAsync("/salas")).Status(), Is.EqualTo(HttpStatusCode.OK));

        var pedido = await cliente.PostAsync("/conta/esqueci-senha", new() { ["Email"] = "jonas@exemplo.com" }, "/conta/esqueci-senha");
        var caminho = CaminhoDoLink(fabrica.Email.Enviados.Last().Corpo);
        var formulario = await cliente.GetAsync(caminho);
        var token = Regex.Match(caminho, "token=([^&]+)").Groups[1].Value;
        var fraca = await cliente.PostAsync("/conta/redefinir-senha", new() { ["Token"] = token, ["NovaSenha"] = "curta" }, caminho);
        var troca = await cliente.PostAsync("/conta/redefinir-senha", new() { ["Token"] = token, ["NovaSenha"] = "outra-senha-bem-longa" }, caminho);
        var velha = await cliente.EntrarAsync("jonas@exemplo.com");
        var nova = await cliente.EntrarAsync("jonas@exemplo.com", "outra-senha-bem-longa");

        Assert.That(await pedido.CorpoAsync(), Does.Contain("Se o e-mail existir"));
        Assert.That(formulario.Status(), Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await fraca.CorpoAsync(), Does.Contain("Senha fraca"));
        Assert.That(troca.Local(), Does.Contain("aviso=senha"));
        Assert.That(await velha.CorpoAsync(), Does.Contain("inválidos"));
        Assert.That(nova.Local(), Is.EqualTo("/salas"));
        Assert.That((await outroAparelho.GetAsync("/salas")).Local(), Does.StartWith("/conta/entrar"));
    }

    [Test]
    public async Task Deve_AvisarQuandoOLinkDeConfirmacaoEInvalido()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();

        var pagina = await cliente.TextoAsync("/conta/confirmar?token=invalido");

        Assert.That(pagina, Does.Contain("Link inválido ou expirado"));
    }

    // ---------- Google ----------

    [Test]
    public async Task Google_Deve_DarNotFound_QuandoNaoConfigurado()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();

        Assert.That((await cliente.GetAsync("/conta/google")).Status(), Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await cliente.TextoAsync("/conta/entrar"), Does.Not.Contain("/conta/google"));
    }

    [Test]
    public async Task Google_Deve_ConcluirCadastro_EDepoisEntrarDireto()
    {
        await using var fabrica = new FabricaComGoogleFalso();
        var cliente = fabrica.NovoCliente();
        await cliente.GetAsync("/teste/externo?id=g1&email=ana@exemplo.com&nome=Ana&verificado=true");

        var retorno = await cliente.GetAsync("/conta/google-retorno");
        var termos = await cliente.TextoAsync("/conta/termos");
        var semAceite = await cliente.PostAsync("/conta/termos", new() { ["Apelido"] = "Ana" }, "/conta/termos");
        var concluido = await cliente.PostAsync("/conta/termos",
            new() { ["Apelido"] = "Ana", ["AceitoTermos"] = "true", ["DeclaroMaioridade"] = "true" }, "/conta/termos");
        var lobby = await cliente.TextoAsync("/salas");

        var segundo = fabrica.NovoCliente();
        await segundo.GetAsync("/teste/externo?id=g1&email=ana@exemplo.com&nome=Ana&verificado=true");
        var direto = await segundo.GetAsync("/conta/google-retorno");

        Assert.That(retorno.Local(), Is.EqualTo("/conta/termos"));
        Assert.That(termos, Does.Contain("value=\"Ana\""));
        Assert.That(await semAceite.CorpoAsync(), Does.Contain("É preciso aceitar"));
        Assert.That(concluido.Local(), Is.EqualTo("/salas"));
        Assert.That(lobby, Does.Not.Contain("Confirme seu e-mail para poder"));
        Assert.That(direto.Local(), Is.EqualTo("/salas"));
    }

    [Test]
    public async Task Google_Deve_RecusarEmailNaoVerificado_ESemRetornoExterno()
    {
        await using var fabrica = new FabricaComGoogleFalso();
        var cliente = fabrica.NovoCliente();

        var semExterno = await cliente.GetAsync("/conta/google-retorno");
        var termosSemExterno = await cliente.GetAsync("/conta/termos");
        var postSemExterno = await cliente.PostAsync("/conta/termos", new() { ["Apelido"] = "Ana" });
        await cliente.GetAsync("/teste/externo?id=g2&email=bia@exemplo.com&nome=Bia&verificado=false");
        var naoVerificado = await cliente.GetAsync("/conta/google-retorno");

        Assert.That(semExterno.Local(), Is.EqualTo("/conta/entrar"));
        Assert.That(termosSemExterno.Local(), Is.EqualTo("/conta/entrar"));
        Assert.That(postSemExterno.Local(), Is.EqualTo("/conta/entrar"));
        Assert.That(await naoVerificado.CorpoAsync(), Does.Contain("não está verificado"));
    }

    [Test]
    public async Task Deve_LimitarRequisicoesDasTelasDeConta()
    {
        await using var fabrica = new BestiaFactory();
        fabrica.Configuracoes["Bestia:RequisicoesPorMinutoNasContas"] = "3";
        var cliente = fabrica.NovoCliente();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            statuses.Add((await cliente.EntrarAsync("a@exemplo.com", "qualquer-coisa-aqui")).Status());

        Assert.That(statuses.Take(3), Has.All.EqualTo(HttpStatusCode.OK));
        Assert.That(statuses.Skip(3), Has.All.EqualTo(HttpStatusCode.TooManyRequests));
    }

    [Test]
    public async Task Google_Deve_AceitarIdentidadeSemNomeNemEmail_ESugerirApelidoVazio()
    {
        await using var fabrica = new FabricaComGoogleFalso();
        var cliente = fabrica.NovoCliente();
        await cliente.GetAsync("/teste/externo?verificado=true");

        var retorno = await cliente.GetAsync("/conta/google-retorno");
        var termos = await cliente.TextoAsync("/conta/termos");

        Assert.That(retorno.Local(), Is.EqualTo("/conta/termos"));
        Assert.That(termos, Does.Contain("name=\"Apelido\""));
    }

    [Test]
    public async Task Google_Deve_RedirecionarParaOGoogle_QuandoConfigurado()
    {
        await using var fabrica = new BestiaFactory();
        fabrica.Configuracoes["Google:ClientId"] = "cliente-de-teste";
        fabrica.Configuracoes["Google:ClientSecret"] = "segredo-de-teste";
        var cliente = fabrica.NovoCliente();

        var entrar = await cliente.TextoAsync("/conta/entrar");
        var desafio = await cliente.GetAsync("/conta/google");

        Assert.That(entrar, Does.Contain("/conta/google"));
        Assert.That(desafio.Status(), Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(desafio.Headers.Location!.Host, Is.EqualTo("accounts.google.com"));
    }

    [Test]
    public async Task Deve_UsarOIpDaConexao_NosRegistrosDeAcesso()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = fabrica.NovoCliente();
        await cliente.CadastrarAsync("Jonas", "jonas@exemplo.com");
        var pedido = new HttpRequestMessage(HttpMethod.Get, "/conta/entrar");
        pedido.Headers.Add("X-Teste-Ip", "203.0.113.7");

        var resposta = await cliente.EnviarAsync(pedido);
        await cliente.PostAsync("/conta/entrar", new() { ["Email"] = "jonas@exemplo.com", ["Senha"] = "uma-senha-bem-longa" });

        Assert.That(resposta.Status(), Is.EqualTo(HttpStatusCode.OK));
        Assert.That(fabrica.Repositorio.Todos<RegistroDeAcesso>().Count(), Is.EqualTo(1));
    }
}
