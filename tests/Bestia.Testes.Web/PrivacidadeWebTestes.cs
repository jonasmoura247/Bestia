using System.Net;
using Bestia.Aplicacao.Modelos;
using Bestia.Aplicacao.Contratos;
using Bestia.Aplicacao.Privacidade;
using Bestia.Testes.Aplicacao;
using Bestia.Web.Configuracao;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bestia.Testes.Web;

public class PrivacidadeWebTestes
{
    private static async Task<ClienteDeTeste> LogadoAsync(BestiaFactory fabrica, string apelido = "Jonas", string email = "jonas@exemplo.com")
    {
        var cliente = fabrica.NovoCliente();
        await cliente.CadastrarAsync(apelido, email);
        await cliente.EntrarAsync(email);
        return cliente;
    }

    private const string Pagina = "/configuracoes/privacidade";

    [Test]
    public async Task Deve_MostrarOsMeusDados_EBaixarEmJson()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = await LogadoAsync(fabrica);

        var pagina = await cliente.TextoAsync(Pagina);
        var exportacao = await cliente.GetAsync(Pagina + "/exportar");

        Assert.That(pagina, Does.Contain("jonas@exemplo.com"));
        Assert.That(pagina, Does.Contain("Baixar meus dados"));
        Assert.That(exportacao.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
        Assert.That(exportacao.Content.Headers.ContentDisposition!.FileName, Is.EqualTo("meus-dados-bestia.json"));
        Assert.That(await exportacao.CorpoAsync(), Does.Contain("jonas@exemplo.com"));
    }

    [Test]
    public async Task Deve_AlterarApelido_ERefletirNoMenu()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = await LogadoAsync(fabrica);

        var ruim = await cliente.PostAsync(Pagina + "/apelido", new() { ["novoApelido"] = "ab" }, Pagina);
        var erro = await cliente.TextoAsync(ruim.Local());
        var bom = await cliente.PostAsync(Pagina + "/apelido", new() { ["novoApelido"] = "Jonas Novo" }, Pagina);
        var pagina = await cliente.TextoAsync(bom.Local());

        Assert.That(erro, Does.Contain("de 3 a 16 caracteres"));
        Assert.That(bom.Local(), Does.Contain("ok=apelido"));
        Assert.That(pagina, Does.Contain("Apelido atualizado"));
        Assert.That(pagina, Does.Contain("Jonas Novo"));
    }

    [Test]
    public async Task Deve_AlterarEmail_ExigindoASenha()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = await LogadoAsync(fabrica);
        fabrica.Email.Enviados.Clear();

        var errada = await cliente.PostAsync(Pagina + "/email", new() { ["novoEmail"] = "novo@exemplo.com", ["senha"] = "errada-errada" }, Pagina);
        var certa = await cliente.PostAsync(Pagina + "/email", new() { ["novoEmail"] = "novo@exemplo.com", ["senha"] = "uma-senha-bem-longa" }, Pagina);

        Assert.That(await (await cliente.GetAsync(errada.Local())).CorpoAsync(), Does.Contain("Senha incorreta"));
        Assert.That(certa.Local(), Does.Contain("ok=email"));
        Assert.That(fabrica.Email.Enviados.Single().Para, Is.EqualTo("novo@exemplo.com"));
    }

    [Test]
    public async Task Deve_RegistrarPedidoAoEncarregado()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = await LogadoAsync(fabrica);

        var resposta = await cliente.PostAsync(Pagina + "/solicitar", new() { ["tipo"] = "Correcao" }, Pagina);

        Assert.That(resposta.Local(), Does.Contain("ok=solicitacao"));
        Assert.That(fabrica.Repositorio.Todos<SolicitacaoPrivacidade>().Single().Tipo, Is.EqualTo(TipoDeSolicitacao.Correcao));
    }

    [Test]
    public async Task Deve_EncerrarAConta_AoRetirarOConsentimento()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = await LogadoAsync(fabrica);

        var resposta = await cliente.PostAsync(Pagina + "/revogar", new() { ["documento"] = "Termos" }, Pagina);
        var depois = await cliente.GetAsync("/salas");

        Assert.That(resposta.Local(), Does.Contain("aviso=excluida"));
        Assert.That(depois.Local(), Does.StartWith("/conta/entrar"));
        Assert.That(fabrica.Repositorio.Todos<Jogador>().Single().RemovidoEm, Is.Not.Null);
    }

    [Test]
    public async Task Deve_ExcluirAConta_SoComASenhaCerta_EDeslogar()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = await LogadoAsync(fabrica);

        var errada = await cliente.PostAsync(Pagina + "/excluir", new() { ["senha"] = "errada-errada" }, Pagina);
        var certa = await cliente.PostAsync(Pagina + "/excluir", new() { ["senha"] = "uma-senha-bem-longa" }, Pagina);
        var aviso = await cliente.TextoAsync(certa.Local());
        var tentarEntrar = await fabrica.NovoCliente().EntrarAsync("jonas@exemplo.com");

        Assert.That(errada.Local(), Does.Contain("erro=Erro_SenhaIncorreta"));
        Assert.That(certa.Local(), Does.Contain("aviso=excluida"));
        Assert.That(aviso, Does.Contain("Conta excluída"));
        Assert.That(await tentarEntrar.CorpoAsync(), Does.Contain("inválidos"));
        var conta = fabrica.Repositorio.Todos<Jogador>().Single();
        Assert.That(conta.EmailCifrado, Is.Null);
        Assert.That(conta.SenhaHash, Is.Null);
    }

    [Test]
    public async Task Deve_SairDaSalaAntesDeExcluirAConta()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = await LogadoAsync(fabrica);
        foreach (var conta in fabrica.Repositorio.Todos<Jogador>())
            conta.EmailConfirmadoEm = fabrica.Relogio.AgoraUtc;
        await cliente.PostAsync("/salas/criar", new() { ["Nome"] = "Mesa do canto", ["Maximo"] = "4" }, "/salas");

        await cliente.PostAsync(Pagina + "/excluir", new() { ["senha"] = "uma-senha-bem-longa" }, Pagina);

        Assert.That(fabrica.Repositorio.Todos<Sala>().Single().Situacao, Is.EqualTo(SituacaoDaSala.Encerrada));
    }

    // ---------- contas só do Google ----------

    private static async Task<(FabricaComGoogleFalso Fabrica, ClienteDeTeste Cliente)> ContaGoogleAsync()
    {
        var fabrica = new FabricaComGoogleFalso();
        fabrica.Configuracoes["Google:ClientId"] = "cliente-de-teste";
        fabrica.Configuracoes["Google:ClientSecret"] = "segredo-de-teste";
        var cliente = fabrica.NovoCliente();
        await cliente.GetAsync("/teste/externo?id=g1&email=ana@exemplo.com&nome=Ana&verificado=true");
        await cliente.GetAsync("/conta/google-retorno");
        await cliente.PostAsync("/conta/termos",
            new() { ["Apelido"] = "Ana", ["AceitoTermos"] = "true", ["DeclaroMaioridade"] = "true" }, "/conta/termos");
        return (fabrica, cliente);
    }

    [Test]
    public async Task Google_Deve_OferecerExclusaoComNovaAutenticacao()
    {
        var (fabrica, cliente) = await ContaGoogleAsync();
        await using var _ = fabrica;

        var pagina = await cliente.TextoAsync(Pagina);
        var desafio = await cliente.GetAsync(Pagina + "/excluir-google");

        Assert.That(pagina, Does.Contain("excluir-google"));
        Assert.That(pagina, Does.Not.Contain("name=\"novoEmail\""));
        Assert.That(desafio.Status(), Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(desafio.Headers.Location!.Host, Is.EqualTo("accounts.google.com"));
    }

    [Test]
    public async Task Google_Deve_ExcluirQuandoAIdentidadeConfere_ERecusarQuandoNao()
    {
        var (fabrica, cliente) = await ContaGoogleAsync();
        await using var _ = fabrica;

        await cliente.GetAsync("/teste/externo?id=outra&email=x@exemplo.com&nome=X&verificado=true");
        var recusada = await cliente.GetAsync(Pagina + "/excluir-google-retorno");
        var semExterno = await cliente.GetAsync(Pagina + "/excluir-google-retorno");
        await cliente.GetAsync("/teste/externo?id=g1&email=ana@exemplo.com&nome=Ana&verificado=true");
        var aceita = await cliente.GetAsync(Pagina + "/excluir-google-retorno");

        Assert.That(recusada.Local(), Does.Contain("erro=Erro_SenhaIncorreta"));
        Assert.That(semExterno.Local(), Does.Contain("erro=Erro_SenhaIncorreta"));
        Assert.That(aceita.Local(), Does.Contain("aviso=excluida"));
        Assert.That(fabrica.Repositorio.Todos<Jogador>().Single().RemovidoEm, Is.Not.Null);
    }

    [Test]
    public async Task Google_NaoConfigurado_Deve_DarNotFoundNaExclusao()
    {
        await using var fabrica = new BestiaFactory();
        var cliente = await LogadoAsync(fabrica);

        var resposta = await cliente.GetAsync(Pagina + "/excluir-google");

        Assert.That(resposta.Status(), Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ---------- rotina diária ----------

    [Test]
    public async Task Rotinas_Deve_RodarAoIniciar_ERepetirACadaVinteEQuatroHoras()
    {
        var repositorio = new RepositorioEmMemoria();
        var relogio = new RelogioFalso();
        var servico = new RotinasDeRetencao(
            new UnidadeDeTrabalhoFalsa(repositorio), new CriptografiaFalsa(), new HashDeSenhaFalso(), relogio,
            new EnviadorDeEmailFalso(), new Bestia.Recursos.ProvedorDeTextos(), NullLogger<RotinasDeRetencao>.Instance);
        repositorio.Adicionar(new RegistroDeAcesso { Id = Guid.NewGuid(), JogadorId = Guid.NewGuid(), OcorreuEm = relogio.AgoraUtc.AddMonths(-8) });
        await repositorio.SalvarAsync();

        await servico.StartAsync(CancellationToken.None);
        await EsperarAsync(() => !repositorio.Todos<RegistroDeAcesso>().Any());
        repositorio.Adicionar(new RegistroDeAcesso { Id = Guid.NewGuid(), JogadorId = Guid.NewGuid(), OcorreuEm = relogio.AgoraUtc.AddMonths(-8) });
        await repositorio.SalvarAsync();
        await EsperarAsync(() => relogio.Esperas == 1);
        relogio.Avancar(RotinasDeRetencao.Intervalo);
        await EsperarAsync(() => !repositorio.Todos<RegistroDeAcesso>().Any());
        await servico.StopAsync(CancellationToken.None);

        Assert.That(repositorio.Todos<RegistroDeAcesso>(), Is.Empty);
    }

    [Test]
    public async Task Rotinas_Deve_RegistrarAFalha_ESeguirViva()
    {
        var repositorio = new RepositorioEmMemoria();
        var relogio = new RelogioFalso();
        var trabalho = new UnidadeDeTrabalhoFalsa(repositorio) { ChamadasAteFalhar = 0 };
        var servico = new RotinasDeRetencao(
            trabalho, new CriptografiaFalsa(), new HashDeSenhaFalso(), relogio,
            new EnviadorDeEmailFalso(), new Bestia.Recursos.ProvedorDeTextos(), NullLogger<RotinasDeRetencao>.Instance);

        await servico.StartAsync(CancellationToken.None);
        await EsperarAsync(() => relogio.Esperas == 1);
        await servico.StopAsync(CancellationToken.None);

        Assert.That(trabalho.Chamadas, Is.EqualTo(1));
    }

    private static async Task EsperarAsync(Func<bool> condicao)
    {
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (!condicao())
        {
            Assert.That(DateTime.UtcNow, Is.LessThan(limite), "tempo esgotado esperando a condição");
            await Task.Delay(10);
        }
    }
}
