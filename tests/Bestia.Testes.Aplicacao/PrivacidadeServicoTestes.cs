using Bestia.Aplicacao.Contas;
using Bestia.Aplicacao.Modelos;
using Bestia.Aplicacao.Privacidade;
using Bestia.Dominio;
using Bestia.Recursos;
using NUnit.Framework;

namespace Bestia.Testes.Aplicacao;

public class PrivacidadeServicoTestes
{
    private RepositorioEmMemoria _repositorio = null!;
    private RelogioFalso _relogio = null!;
    private EnviadorDeEmailFalso _email = null!;
    private CriptografiaFalsa _criptografia = null!;
    private ContaServico _contas = null!;
    private PrivacidadeServico _servico = null!;

    [SetUp]
    public void Preparar()
    {
        _repositorio = new RepositorioEmMemoria();
        _relogio = new RelogioFalso();
        _email = new EnviadorDeEmailFalso();
        _criptografia = new CriptografiaFalsa();
        var hash = new HashDeSenhaFalso();
        var textos = new ProvedorDeTextos();
        _contas = new ContaServico(_repositorio, _criptografia, hash, _relogio, _email, textos);
        _servico = new PrivacidadeServico(_repositorio, _criptografia, hash, _relogio, _email, textos);
    }

    private async Task<Guid> NovaContaAsync(string apelido = "Jonas", string email = "jonas@exemplo.com")
    {
        await _contas.CriarContaAsync(new PedidoDeCadastro(
            apelido, email, "uma-senha-longa", true, true, "pt-BR", "10.0.0.1", t => "https://x/c?t=" + t));
        return _repositorio.Todos<Jogador>().Single(j => j.EmailHash!.SequenceEqual(_criptografia.IndiceCego(email))).Id;
    }

    private async Task<Guid> ContaDoGoogleAsync()
    {
        var google = new IdentidadeGoogle("g-1", "ana@exemplo.com", true, "Ana");
        var sessao = await _contas.ConcluirCadastroComGoogleAsync(google, "Ana", true, true, "pt-BR", "ip");
        return sessao.Valor!.Id;
    }

    // ---------- meus dados e exportação ----------

    [Test]
    public async Task Deve_MostrarOsDadosDaPessoa_JaDecifrados()
    {
        var id = await NovaContaAsync();
        _repositorio.Adicionar(new PartidaJogador { PartidaId = Guid.NewGuid(), Assento = 0, JogadorId = id });
        await _repositorio.SalvarAsync();

        var dados = await _servico.MeusDadosAsync(id);

        Assert.That(dados!.Apelido, Is.EqualTo("Jonas"));
        Assert.That(dados.Email, Is.EqualTo("jonas@exemplo.com"));
        Assert.That(dados.TemSenha, Is.True);
        Assert.That(dados.Consentimentos.Select(c => c.Documento), Is.EquivalentTo(new[] { "Termos", "Privacidade" }));
        Assert.That(dados.Partidas, Is.EqualTo(1));
        Assert.That(await _servico.MeusDadosAsync(Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task Deve_ListarLoginsEPartidas_NaExportacaoDeUmaContaDoGoogle()
    {
        var id = await ContaDoGoogleAsync();
        _repositorio.Adicionar(new PartidaJogador { PartidaId = Guid.NewGuid(), Assento = 2, JogadorId = id, FichasFinais = 33, Colocacao = 1 });
        _repositorio.Adicionar(new SolicitacaoPrivacidade { Id = Guid.NewGuid(), JogadorId = id, Tipo = TipoDeSolicitacao.Acesso });
        await _repositorio.SalvarAsync();

        var dados = await _servico.MeusDadosAsync(id);
        var exportacao = await _servico.ExportarAsync(id);

        Assert.That(dados!.Logins, Is.EqualTo(new[] { "Google" }));
        Assert.That(dados.TemSenha, Is.False);
        Assert.That(exportacao.Valor, Does.Contain("\"FichasFinais\": 33"));
        Assert.That(exportacao.Valor, Does.Contain("\"Acesso\"").Or.Contain("0"));
    }

    [Test]
    public async Task Deve_ExportarEmJson_ERegistrarAPortabilidade()
    {
        var id = await NovaContaAsync();

        var exportacao = await _servico.ExportarAsync(id);

        Assert.That(exportacao.Valor, Does.Contain("jonas@exemplo.com"));
        Assert.That(exportacao.Valor, Does.Contain("\"Termos\""));
        var solicitacao = _repositorio.Todos<SolicitacaoPrivacidade>().Single();
        Assert.That(solicitacao.Tipo, Is.EqualTo(TipoDeSolicitacao.Portabilidade));
        Assert.That(solicitacao.Situacao, Is.EqualTo(SituacaoDaSolicitacao.Respondida));
        Assert.That(_repositorio.Todos<RegistroDeAuditoria>().Any(a => a.Acao == "DadosExportados"), Is.True);
        Assert.That((await _servico.ExportarAsync(Guid.NewGuid())).ChaveDoErro, Is.EqualTo(ChavesDeErro.ContaNaoEncontrada));
    }

    // ---------- correção ----------

    [Test]
    public async Task Deve_AlterarOApelido_ValidandoTamanhoEUnicidade()
    {
        var id = await NovaContaAsync();
        await NovaContaAsync("Bia", "bia@exemplo.com");

        var curto = await _servico.AlterarApelidoAsync(id, "ab");
        var emUso = await _servico.AlterarApelidoAsync(id, "bia");
        var mesmo = await _servico.AlterarApelidoAsync(id, "JONAS");
        var novo = await _servico.AlterarApelidoAsync(id, "Jonas Novo");
        var semConta = await _servico.AlterarApelidoAsync(Guid.NewGuid(), "Qualquer");

        Assert.That(curto.ChaveDoErro, Is.EqualTo(ChavesDeErro.ApelidoInvalido));
        Assert.That(emUso.ChaveDoErro, Is.EqualTo(ChavesDeErro.ApelidoEmUso));
        Assert.That(mesmo.Sucesso, Is.True);
        Assert.That(novo.Sucesso, Is.True);
        Assert.That(semConta.ChaveDoErro, Is.EqualTo(ChavesDeErro.ContaNaoEncontrada));
        Assert.That((await _servico.MeusDadosAsync(id))!.Apelido, Is.EqualTo("Jonas Novo"));
    }

    [Test]
    public async Task Deve_AlterarOEmail_ExigindoSenha_ENovaConfirmacao()
    {
        var id = await NovaContaAsync();
        await NovaContaAsync("Bia", "bia@exemplo.com");
        await _contas.ConfirmarEmailAsync(Token(_email.Enviados[0].Corpo));
        _email.Enviados.Clear();
        string Link(string t) => "https://x/c?t=" + t;

        var senhaErrada = await _servico.AlterarEmailAsync(id, "novo@exemplo.com", "errada-errada-aqui", Link);
        var invalido = await _servico.AlterarEmailAsync(id, "sem-arroba", "uma-senha-longa", Link);
        var emUso = await _servico.AlterarEmailAsync(id, "bia@exemplo.com", "uma-senha-longa", Link);
        var ok = await _servico.AlterarEmailAsync(id, "novo@exemplo.com", "uma-senha-longa", Link);
        var semConta = await _servico.AlterarEmailAsync(Guid.NewGuid(), "x@exemplo.com", "uma-senha-longa", Link);

        Assert.That(senhaErrada.ChaveDoErro, Is.EqualTo(ChavesDeErro.SenhaIncorreta));
        Assert.That(invalido.ChaveDoErro, Is.EqualTo(ChavesDeErro.EmailInvalido));
        Assert.That(emUso.ChaveDoErro, Is.EqualTo(ChavesDeErro.EmailEmUso));
        Assert.That(ok.Sucesso, Is.True);
        Assert.That(semConta.ChaveDoErro, Is.EqualTo(ChavesDeErro.ContaNaoEncontrada));
        Assert.That(_email.Enviados.Single().Para, Is.EqualTo("novo@exemplo.com"));
        var dados = (await _servico.MeusDadosAsync(id))!;
        Assert.That(dados.Email, Is.EqualTo("novo@exemplo.com"));
        Assert.That(dados.EmailConfirmadoEm, Is.Null);
        var confirmacao = await _contas.ConfirmarEmailAsync(Token(_email.Enviados.Single().Corpo));
        Assert.That(confirmacao.Sucesso, Is.True);
    }

    [Test]
    public async Task Deve_RecusarTrocaDeEmail_ParaContaSemSenha()
    {
        var id = await ContaDoGoogleAsync();

        var resultado = await _servico.AlterarEmailAsync(id, "novo@exemplo.com", "qualquer", t => t);

        Assert.That(resultado.ChaveDoErro, Is.EqualTo(ChavesDeErro.OperacaoRequerSenha));
    }

    private static string Token(string corpo) => corpo[(corpo.IndexOf("?t=", StringComparison.Ordinal) + 3)..].Trim();

    // ---------- direitos do titular ----------

    [Test]
    public async Task Deve_RegistrarPedidoAoEncarregado_ComPrazoDeQuinzeDias()
    {
        var id = await NovaContaAsync();

        var ok = await _servico.SolicitarAoEncarregadoAsync(id, TipoDeSolicitacao.Correcao);
        var semConta = await _servico.SolicitarAoEncarregadoAsync(Guid.NewGuid(), TipoDeSolicitacao.Acesso);

        var pedido = _repositorio.Todos<SolicitacaoPrivacidade>().Single();
        Assert.That(ok.Sucesso, Is.True);
        Assert.That(pedido.Situacao, Is.EqualTo(SituacaoDaSolicitacao.Aberta));
        Assert.That(pedido.PrazoEm, Is.EqualTo(pedido.CriadaEm.AddDays(15)));
        Assert.That(semConta.ChaveDoErro, Is.EqualTo(ChavesDeErro.ContaNaoEncontrada));
    }

    [Test]
    public async Task Deve_EncerrarAConta_AoRetirarConsentimentoEssencial()
    {
        var id = await NovaContaAsync();

        var resultado = await _servico.RevogarConsentimentoAsync(id, "Termos");
        var semConta = await _servico.RevogarConsentimentoAsync(Guid.NewGuid(), "Termos");

        Assert.That(resultado.Sucesso, Is.True);
        Assert.That(semConta.ChaveDoErro, Is.EqualTo(ChavesDeErro.ContaNaoEncontrada));
        var termos = _repositorio.Todos<Consentimento>().Single(c => c.Documento == "Termos");
        Assert.That(termos.RevogadoEm, Is.Not.Null);
        Assert.That(_repositorio.Todos<Jogador>().Single().RemovidoEm, Is.Not.Null);
    }

    [Test]
    public async Task Deve_ExcluirAConta_ApagandoOQueIdentifica_EManterAUnicaLinha()
    {
        var id = await NovaContaAsync();
        var conta = _repositorio.Todos<Jogador>().Single();
        var carimbo = conta.CarimboDeSeguranca;
        await _contas.EntrarAsync("jonas@exemplo.com", "uma-senha-longa", "10.0.0.1");

        var errada = await _servico.ExcluirContaAsync(id, "senha-errada-aqui", reautenticouComGoogle: false);
        var semSenha = await _servico.ExcluirContaAsync(id, null, reautenticouComGoogle: true);
        var ok = await _servico.ExcluirContaAsync(id, "uma-senha-longa", reautenticouComGoogle: false);
        var denovo = await _servico.ExcluirContaAsync(id, "uma-senha-longa", false);

        Assert.That(errada.ChaveDoErro, Is.EqualTo(ChavesDeErro.SenhaIncorreta));
        Assert.That(semSenha.ChaveDoErro, Is.EqualTo(ChavesDeErro.SenhaIncorreta));
        Assert.That(ok.Sucesso, Is.True);
        Assert.That(denovo.ChaveDoErro, Is.EqualTo(ChavesDeErro.ContaNaoEncontrada));
        Assert.That(conta.EmailCifrado, Is.Null);
        Assert.That(conta.EmailHash, Is.Null);
        Assert.That(conta.ApelidoCifrado, Is.Null);
        Assert.That(conta.ApelidoHash, Is.Null);
        Assert.That(conta.SenhaHash, Is.Null);
        Assert.That(conta.RemovidoEm, Is.Not.Null);
        Assert.That(conta.CarimboDeSeguranca, Is.Not.EqualTo(carimbo));
        Assert.That(_repositorio.Todos<RegistroDeAcesso>(), Is.Empty);
        Assert.That(_repositorio.Todos<TokenDeConta>(), Is.Empty);
        Assert.That(_repositorio.Todos<Consentimento>().Count(), Is.EqualTo(2));
        Assert.That(_repositorio.Todos<SolicitacaoPrivacidade>().Single().Tipo, Is.EqualTo(TipoDeSolicitacao.Eliminacao));
    }

    [Test]
    public async Task Deve_ExcluirContaDoGoogle_SoComNovaAutenticacao()
    {
        var id = await ContaDoGoogleAsync();

        var semAutenticar = await _servico.ExcluirContaAsync(id, null, reautenticouComGoogle: false);
        var autenticado = await _servico.ExcluirContaAsync(id, null, reautenticouComGoogle: true);

        Assert.That(semAutenticar.ChaveDoErro, Is.EqualTo(ChavesDeErro.SenhaIncorreta));
        Assert.That(autenticado.Sucesso, Is.True);
        Assert.That(_repositorio.Todos<LoginExterno>(), Is.Empty);
    }

    [Test]
    public async Task Deve_ConfirmarAIdentidadeGoogleDaConta()
    {
        var id = await ContaDoGoogleAsync();

        Assert.That(await _servico.ConfirmarIdentidadeGoogleAsync(id, "g-1"), Is.True);
        Assert.That(await _servico.ConfirmarIdentidadeGoogleAsync(id, "outro"), Is.False);
        Assert.That(await _servico.ConfirmarIdentidadeGoogleAsync(Guid.NewGuid(), "g-1"), Is.False);
    }

    // ---------- retenção ----------

    [Test]
    public async Task Rotinas_Deve_ApagarAcessosAntigosETokensVencidos()
    {
        var id = await NovaContaAsync();
        var agora = _relogio.AgoraUtc;
        _repositorio.Adicionar(new RegistroDeAcesso { Id = Guid.NewGuid(), JogadorId = id, OcorreuEm = agora.AddMonths(-7) });
        _repositorio.Adicionar(new RegistroDeAcesso { Id = Guid.NewGuid(), JogadorId = id, OcorreuEm = agora.AddMonths(-1) });
        _repositorio.Adicionar(new TokenDeConta { Id = Guid.NewGuid(), JogadorId = id, ExpiraEm = agora.AddDays(-2) });
        await _repositorio.SalvarAsync();

        await _servico.ExecutarRotinasAsync();

        Assert.That(_repositorio.Todos<RegistroDeAcesso>().Count(), Is.EqualTo(1));
        Assert.That(_repositorio.Todos<TokenDeConta>().Count(), Is.EqualTo(1), "o token válido do cadastro continua");
        Assert.That(_repositorio.Todos<Jogador>().Single().RemovidoEm, Is.Null);
    }

    [Test]
    public async Task Rotinas_Deve_AvisarDepoisExcluir_ContasInativasHaVinteEQuatroMeses()
    {
        var id = await NovaContaAsync();
        var conta = _repositorio.Todos<Jogador>().Single();
        conta.UltimoAcessoEm = _relogio.AgoraUtc.AddMonths(-25);
        _email.Enviados.Clear();

        await _servico.ExecutarRotinasAsync();
        var aposAviso = conta.RemovidoEm;
        _relogio.Avancar(TimeSpan.FromDays(10));
        await _servico.ExecutarRotinasAsync();
        var aposDezDias = conta.RemovidoEm;
        _relogio.Avancar(TimeSpan.FromDays(21));
        await _servico.ExecutarRotinasAsync();

        Assert.That(_email.Enviados.Single().Para, Is.EqualTo("jonas@exemplo.com"));
        Assert.That(_email.Enviados.Single().Corpo, Does.Contain("30 dias"));
        Assert.That(aposAviso, Is.Null);
        Assert.That(aposDezDias, Is.Null);
        Assert.That(conta.RemovidoEm, Is.Not.Null);
        Assert.That(conta.EmailCifrado, Is.Null);
        Assert.That(id, Is.EqualTo(conta.Id));
    }

    [Test]
    public async Task Rotinas_Deve_UsarADataDeCriacao_QuandoNuncaHouveAcesso_EPouparContasAtivas()
    {
        await NovaContaAsync();
        var antiga = _repositorio.Todos<Jogador>().Single();
        antiga.CriadoEm = _relogio.AgoraUtc.AddMonths(-30);
        await NovaContaAsync("Bia", "bia@exemplo.com");
        _email.Enviados.Clear();

        await _servico.ExecutarRotinasAsync();

        Assert.That(_email.Enviados.Select(e => e.Para), Is.EqualTo(new[] { "jonas@exemplo.com" }));
    }
}
