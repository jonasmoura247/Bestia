using Bestia.Aplicacao.Contas;
using Bestia.Aplicacao.Modelos;
using Bestia.Dominio;
using Bestia.Recursos;
using NUnit.Framework;

namespace Bestia.Testes.Aplicacao;

public class ContaServicoTestes
{
    private RepositorioEmMemoria _repositorio = null!;
    private RelogioFalso _relogio = null!;
    private EnviadorDeEmailFalso _email = null!;
    private CriptografiaFalsa _criptografia = null!;
    private ContaServico _servico = null!;

    [SetUp]
    public void Preparar()
    {
        _repositorio = new RepositorioEmMemoria();
        _relogio = new RelogioFalso();
        _email = new EnviadorDeEmailFalso();
        _criptografia = new CriptografiaFalsa();
        _servico = new ContaServico(_repositorio, _criptografia, new HashDeSenhaFalso(), _relogio, _email, new ProvedorDeTextos());
    }

    private static string Link(string token) => "https://taverna/confirmar?t=" + token;

    private static string TokenDo(string corpo) => corpo[(corpo.IndexOf("?t=", StringComparison.Ordinal) + 3)..].TrimEnd('.', ' ').Split(' ')[0].TrimEnd('.');

    private static PedidoDeCadastro Pedido(
        string apelido = "Jonas", string email = "jonas@exemplo.com", string senha = "uma-senha-longa",
        bool termos = true, bool maior = true, string? idioma = "en") =>
        new(apelido, email, senha, termos, maior, idioma, "10.0.0.1", Link);

    private async Task<SessaoDeConta> CadastrarEEntrarAsync(string apelido = "Jonas", string email = "jonas@exemplo.com")
    {
        Assert.That((await _servico.CriarContaAsync(Pedido(apelido, email))).Sucesso, Is.True);
        var sessao = await _servico.EntrarAsync(email, "uma-senha-longa", "10.0.0.1");
        return sessao.Valor!;
    }

    private static IdentidadeGoogle Google(string id = "g-1", string email = "ana@exemplo.com", bool verificado = true) =>
        new(id, email, verificado, "Ana");

    // ---------- cadastro ----------

    [Test]
    public async Task Cadastro_Deve_GuardarSoDadosProtegidos_EEnviarLinkNoIdiomaDaPessoa()
    {
        var resultado = await _servico.CriarContaAsync(Pedido());

        Assert.That(resultado.Sucesso, Is.True);
        var conta = _repositorio.Todos<Jogador>().Single();
        Assert.That(conta.SenhaHash, Is.EqualTo("H:uma-senha-longa"));
        Assert.That(conta.Idioma, Is.EqualTo("en"));
        Assert.That(conta.EmailConfirmadoEm, Is.Null);
        Assert.That(_criptografia.Decifrar(conta.EmailCifrado!), Is.EqualTo("jonas@exemplo.com"));
        Assert.That(_repositorio.Todos<Consentimento>().Select(c => c.Documento), Is.EquivalentTo(new[] { "Termos", "Privacidade" }));
        Assert.That(_repositorio.Todos<RegistroDeAuditoria>().Single().Acao, Is.EqualTo("ContaCriada"));
        var (para, assunto, corpo) = _email.Enviados.Single();
        Assert.That(para, Is.EqualTo("jonas@exemplo.com"));
        Assert.That(assunto, Does.Contain("Confirm your email"));
        Assert.That(corpo, Does.Contain("https://taverna/confirmar?t="));
    }

    [TestCase(false, true, ChavesDeErro.AceiteObrigatorio)]
    [TestCase(true, false, ChavesDeErro.MaioridadeObrigatoria)]
    public async Task Cadastro_Deve_ExigirAceiteEMaioridade(bool termos, bool maior, string esperado)
    {
        var resultado = await _servico.CriarContaAsync(Pedido(termos: termos, maior: maior));

        Assert.That(resultado.ChaveDoErro, Is.EqualTo(esperado));
    }

    [TestCase("ab", "jonas@exemplo.com", "uma-senha-longa", ChavesDeErro.ApelidoInvalido)]
    [TestCase("um-apelido-bem-comprido", "jonas@exemplo.com", "uma-senha-longa", ChavesDeErro.ApelidoInvalido)]
    [TestCase("Jonas", "sem-arroba", "uma-senha-longa", ChavesDeErro.EmailInvalido)]
    [TestCase("Jonas", "jonas@semponto", "uma-senha-longa", ChavesDeErro.EmailInvalido)]
    [TestCase("Jonas", "jonas@exemplo.com", "curta", ChavesDeErro.SenhaFraca)]
    [TestCase("Jonas", "jonas@exemplo.com", "1234567890", ChavesDeErro.SenhaFraca)]
    public async Task Cadastro_Deve_ValidarCampos(string apelido, string email, string senha, string esperado)
    {
        var resultado = await _servico.CriarContaAsync(Pedido(apelido, email, senha));

        Assert.That(resultado.ChaveDoErro, Is.EqualTo(esperado));
        Assert.That(_repositorio.Todos<Jogador>(), Is.Empty);
    }

    [Test]
    public async Task Cadastro_Deve_RecusarEmailEApelidoRepetidos()
    {
        await _servico.CriarContaAsync(Pedido());

        var mesmoEmail = await _servico.CriarContaAsync(Pedido(apelido: "Outro", email: " JONAS@exemplo.com "));
        var mesmoApelido = await _servico.CriarContaAsync(Pedido(apelido: "jonas", email: "outro@exemplo.com"));

        Assert.That(mesmoEmail.ChaveDoErro, Is.EqualTo(ChavesDeErro.EmailEmUso));
        Assert.That(mesmoApelido.ChaveDoErro, Is.EqualTo(ChavesDeErro.ApelidoEmUso));
    }

    // ---------- confirmação de e-mail ----------

    [Test]
    public async Task ConfirmarEmail_Deve_MarcarAContaEGastarOToken()
    {
        await _servico.CriarContaAsync(Pedido());
        var token = TokenDo(_email.Enviados.Single().Corpo);

        var primeira = await _servico.ConfirmarEmailAsync(token);
        var segunda = await _servico.ConfirmarEmailAsync(token);

        Assert.That(primeira.Sucesso, Is.True);
        Assert.That(_repositorio.Todos<Jogador>().Single().EmailConfirmadoEm, Is.EqualTo(_relogio.AgoraUtc));
        Assert.That(segunda.ChaveDoErro, Is.EqualTo(ChavesDeErro.TokenInvalido));
    }

    [Test]
    public async Task ConfirmarEmail_Deve_RecusarTokenDesconhecidoEExpirado()
    {
        await _servico.CriarContaAsync(Pedido());
        var token = TokenDo(_email.Enviados.Single().Corpo);
        _relogio.Avancar(TimeSpan.FromHours(25));

        Assert.That((await _servico.ConfirmarEmailAsync("nao-existe")).ChaveDoErro, Is.EqualTo(ChavesDeErro.TokenInvalido));
        Assert.That((await _servico.ConfirmarEmailAsync(token)).ChaveDoErro, Is.EqualTo(ChavesDeErro.TokenInvalido));
    }

    [Test]
    public async Task ConfirmarEmail_Deve_ManterADataQuandoJaEstavaConfirmado()
    {
        await _servico.CriarContaAsync(Pedido());
        var conta = _repositorio.Todos<Jogador>().Single();
        var antes = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        conta.EmailConfirmadoEm = antes;
        var token = TokenDo(_email.Enviados.Single().Corpo);

        await _servico.ConfirmarEmailAsync(token);

        Assert.That(conta.EmailConfirmadoEm, Is.EqualTo(antes));
    }

    // ---------- entrar ----------

    [Test]
    public async Task Entrar_Deve_DevolverSessao_RegistrarAcesso_ELimparFalhas()
    {
        await _servico.CriarContaAsync(Pedido());
        await _servico.EntrarAsync("jonas@exemplo.com", "errada-errada", "10.0.0.1");

        var resultado = await _servico.EntrarAsync(" JONAS@exemplo.com", "uma-senha-longa", "10.0.0.9");

        Assert.That(resultado.Sucesso, Is.True);
        Assert.That(resultado.Valor!.Apelido, Is.EqualTo("Jonas"));
        Assert.That(resultado.Valor.Idioma, Is.EqualTo("en"));
        Assert.That(resultado.Valor.EmailConfirmado, Is.False);
        var conta = _repositorio.Todos<Jogador>().Single();
        Assert.That(conta.FalhasDeLogin, Is.EqualTo(0));
        Assert.That(conta.UltimoAcessoEm, Is.EqualTo(_relogio.AgoraUtc));
        Assert.That(_criptografia.Decifrar(_repositorio.Todos<RegistroDeAcesso>().Single().IpCifrado), Is.EqualTo("10.0.0.9"));
    }

    [Test]
    public async Task Entrar_Deve_DarAMesmaMensagem_ParaEmailDesconhecidoESenhaErrada()
    {
        await _servico.CriarContaAsync(Pedido());

        var desconhecido = await _servico.EntrarAsync("ninguem@exemplo.com", "uma-senha-longa", "ip");
        var errada = await _servico.EntrarAsync("jonas@exemplo.com", "outra-senha-aqui", "ip");

        Assert.That(desconhecido.ChaveDoErro, Is.EqualTo(ChavesDeErro.CredenciaisInvalidas));
        Assert.That(errada.ChaveDoErro, Is.EqualTo(ChavesDeErro.CredenciaisInvalidas));
    }

    [Test]
    public async Task Entrar_Deve_BloquearPorQuinzeMinutos_AposCincoErros()
    {
        await _servico.CriarContaAsync(Pedido());
        for (var i = 0; i < 5; i++)
            await _servico.EntrarAsync("jonas@exemplo.com", "errada-errada", "ip");

        var durante = await _servico.EntrarAsync("jonas@exemplo.com", "uma-senha-longa", "ip");
        _relogio.Avancar(TimeSpan.FromMinutes(16));
        var depois = await _servico.EntrarAsync("jonas@exemplo.com", "uma-senha-longa", "ip");

        Assert.That(durante.ChaveDoErro, Is.EqualTo(ChavesDeErro.CredenciaisInvalidas));
        Assert.That(depois.Sucesso, Is.True);
    }

    [Test]
    public async Task Entrar_Deve_RecusarContaSoDoGoogleEContaRemovida()
    {
        await _servico.ConcluirCadastroComGoogleAsync(Google(), "Ana", true, true, "pt-BR", "ip");
        await _servico.CriarContaAsync(Pedido());
        _repositorio.Todos<Jogador>().Single(j => j.RemovidoEm == null && j.SenhaHash != null).RemovidoEm = _relogio.AgoraUtc;

        var soGoogle = await _servico.EntrarAsync("ana@exemplo.com", "qualquer-coisa-aqui", "ip");
        var removida = await _servico.EntrarAsync("jonas@exemplo.com", "uma-senha-longa", "ip");

        Assert.That(soGoogle.ChaveDoErro, Is.EqualTo(ChavesDeErro.CredenciaisInvalidas));
        Assert.That(removida.ChaveDoErro, Is.EqualTo(ChavesDeErro.CredenciaisInvalidas));
    }

    // ---------- Google ----------

    [Test]
    public async Task Google_Deve_PedirCadastro_NaPrimeiraVez_ERecusarEmailNaoVerificado()
    {
        var novo = await _servico.EntrarComGoogleAsync(Google(), "ip");
        var naoVerificado = await _servico.EntrarComGoogleAsync(Google(verificado: false), "ip");

        Assert.That(novo.PrecisaDoCadastro, Is.True);
        Assert.That(naoVerificado.ChaveDoErro, Is.EqualTo(ChavesDeErro.GoogleEmailNaoVerificado));
    }

    [Test]
    public async Task Google_Deve_CriarContaConfirmada_EDepoisEntrarDireto()
    {
        var concluido = await _servico.ConcluirCadastroComGoogleAsync(Google(), " Ana ", true, true, "es", "ip");
        var entrou = await _servico.EntrarComGoogleAsync(Google(), "ip");

        Assert.That(concluido.Valor!.EmailConfirmado, Is.True);
        Assert.That(concluido.Valor.Apelido, Is.EqualTo("Ana"));
        Assert.That(entrou.Sessao!.Id, Is.EqualTo(concluido.Valor.Id));
        var conta = _repositorio.Todos<Jogador>().Single();
        Assert.That(conta.SenhaHash, Is.Null);
        Assert.That(_repositorio.Todos<LoginExterno>().Single().Provedor, Is.EqualTo("Google"));
    }

    [Test]
    public async Task Google_Deve_LigarAContaLocalQueJaUsaOMesmoEmail()
    {
        await _servico.CriarContaAsync(Pedido(apelido: "Ana", email: "ana@exemplo.com"));

        var resultado = await _servico.ConcluirCadastroComGoogleAsync(Google(), "Outro", true, true, "pt-BR", "ip");

        Assert.That(_repositorio.Todos<Jogador>(), Has.Exactly(1).Items);
        Assert.That(resultado.Valor!.Apelido, Is.EqualTo("Ana"));
        Assert.That(resultado.Valor.EmailConfirmado, Is.True);
    }

    [Test]
    public async Task Google_Deve_ValidarOCadastro()
    {
        await _servico.CriarContaAsync(Pedido(apelido: "Jonas"));

        var semAceite = await _servico.ConcluirCadastroComGoogleAsync(Google(), "Ana", false, true, null, "ip");
        var naoVerificado = await _servico.ConcluirCadastroComGoogleAsync(Google(verificado: false), "Ana", true, true, null, "ip");
        var apelidoCurto = await _servico.ConcluirCadastroComGoogleAsync(Google(), "Al", true, true, null, "ip");
        var apelidoEmUso = await _servico.ConcluirCadastroComGoogleAsync(Google(), "jonas", true, true, null, "ip");

        Assert.That(semAceite.ChaveDoErro, Is.EqualTo(ChavesDeErro.AceiteObrigatorio));
        Assert.That(naoVerificado.ChaveDoErro, Is.EqualTo(ChavesDeErro.GoogleEmailNaoVerificado));
        Assert.That(apelidoCurto.ChaveDoErro, Is.EqualTo(ChavesDeErro.ApelidoInvalido));
        Assert.That(apelidoEmUso.ChaveDoErro, Is.EqualTo(ChavesDeErro.ApelidoEmUso));
    }

    // ---------- recuperação de senha ----------

    [Test]
    public async Task EsqueciSenha_Deve_CalarQuandoOEmailNaoExiste_EEnviarLinkQuandoExiste()
    {
        await _servico.CriarContaAsync(Pedido());
        _email.Enviados.Clear();

        await _servico.EsqueciSenhaAsync("ninguem@exemplo.com", Link);
        Assert.That(_email.Enviados, Is.Empty);
        await _servico.EsqueciSenhaAsync("jonas@exemplo.com", Link);

        Assert.That(_email.Enviados.Single().Assunto, Does.Contain("password reset"));
    }

    [Test]
    public async Task RedefinirSenha_Deve_TrocarASenha_EInvalidarSessoesEToken()
    {
        var sessao = await CadastrarEEntrarAsync();
        await _servico.EsqueciSenhaAsync("jonas@exemplo.com", Link);
        var token = TokenDo(_email.Enviados.Last().Corpo);

        var resultado = await _servico.RedefinirSenhaAsync(token, "outra-senha-bem-longa");
        var reuso = await _servico.RedefinirSenhaAsync(token, "mais-uma-senha-longa");

        Assert.That(resultado.Sucesso, Is.True);
        Assert.That(reuso.ChaveDoErro, Is.EqualTo(ChavesDeErro.TokenInvalido));
        Assert.That(await _servico.SessaoValidaAsync(sessao.Id, sessao.Carimbo), Is.False);
        Assert.That((await _servico.EntrarAsync("jonas@exemplo.com", "outra-senha-bem-longa", "ip")).Sucesso, Is.True);
    }

    [Test]
    public async Task RedefinirSenha_Deve_RecusarSenhaFracaETokenDeOutroTipo()
    {
        await _servico.CriarContaAsync(Pedido());
        var tokenDeConfirmacao = TokenDo(_email.Enviados.Single().Corpo);

        var fraca = await _servico.RedefinirSenhaAsync("qualquer", "curta");
        var tipoErrado = await _servico.RedefinirSenhaAsync(tokenDeConfirmacao, "outra-senha-bem-longa");

        Assert.That(fraca.ChaveDoErro, Is.EqualTo(ChavesDeErro.SenhaFraca));
        Assert.That(tipoErrado.ChaveDoErro, Is.EqualTo(ChavesDeErro.TokenInvalido));
    }

    // ---------- sessão e perfil ----------

    [Test]
    public async Task Sessao_Deve_SerValidaSoComOCarimboAtualEContaAtiva()
    {
        var sessao = await CadastrarEEntrarAsync();

        Assert.That(await _servico.SessaoValidaAsync(sessao.Id, sessao.Carimbo), Is.True);
        Assert.That(await _servico.SessaoValidaAsync(sessao.Id, Guid.NewGuid()), Is.False);
        Assert.That((await _servico.ObterSessaoAsync(sessao.Id))!.Apelido, Is.EqualTo("Jonas"));

        _repositorio.Todos<Jogador>().Single().RemovidoEm = _relogio.AgoraUtc;
        Assert.That(await _servico.SessaoValidaAsync(sessao.Id, sessao.Carimbo), Is.False);
        Assert.That(await _servico.ObterSessaoAsync(sessao.Id), Is.Null);
    }

    [Test]
    public async Task Idioma_Deve_SerAlterado_ENormalizado()
    {
        var sessao = await CadastrarEEntrarAsync();

        var ok = await _servico.AlterarIdiomaAsync(sessao.Id, "ES");
        var invalido = await _servico.AlterarIdiomaAsync(sessao.Id, "klingon");
        var semConta = await _servico.AlterarIdiomaAsync(Guid.NewGuid(), "en");

        Assert.That(ok.Sucesso, Is.True);
        Assert.That(invalido.Sucesso, Is.True);
        Assert.That(_repositorio.Todos<Jogador>().Single().Idioma, Is.EqualTo("pt-BR"));
        Assert.That(semConta.ChaveDoErro, Is.EqualTo(ChavesDeErro.ContaNaoEncontrada));
    }
}
