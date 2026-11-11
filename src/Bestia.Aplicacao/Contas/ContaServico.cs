using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Bestia.Aplicacao.Contratos;
using Bestia.Aplicacao.Modelos;
using Bestia.Dominio;
using Bestia.Recursos;

namespace Bestia.Aplicacao.Contas;

public sealed record PedidoDeCadastro(
    string Apelido, string Email, string Senha, bool AceitouTermos, bool DeclarouMaioridade,
    string? Idioma, string Ip, Func<string, string> MontarLinkDeConfirmacao);

public sealed record SessaoDeConta(Guid Id, string Apelido, string Idioma, bool EmailConfirmado, Guid Carimbo, string Cor);

public sealed record IdentidadeGoogle(string Id, string Email, bool EmailVerificado, string? Nome);

public sealed record ResultadoDoGoogle(SessaoDeConta? Sessao, string? ChaveDoErro, bool PrecisaDoCadastro)
{
    public static ResultadoDoGoogle Entrou(SessaoDeConta sessao) => new(sessao, null, false);

    public static ResultadoDoGoogle Cadastro() => new(null, null, true);

    public static ResultadoDoGoogle Falha(string chave) => new(null, chave, false);
}

public static class PoliticaDeConta
{
    public const int ApelidoMinimo = 3;
    public const int ApelidoMaximo = 16;
    public const int SenhaMinima = 10;
    public const int FalhasParaBloquear = 5;
    public const int MinutosDeBloqueio = 15;
    public const int HorasDoTokenDeEmail = 24;
    public const int HorasDoTokenDeSenha = 1;
    public const string VersaoDosDocumentos = "1.0";
    public const string DocumentoTermos = "Termos";
    public const string DocumentoPrivacidade = "Privacidade";
    public const string ProvedorGoogle = "Google";
}

public sealed class ContaServico(
    IRepositorio repositorio, ICriptografia criptografia, IHashDeSenha hashDeSenha,
    IRelogio relogio, IEnviadorDeEmail enviadorDeEmail, ITextos textos)
{
    private static readonly string[] SenhasComuns =
    [
        "1234567890", "12345678910", "0123456789", "qwertyuiop", "password12", "senha12345",
        "1q2w3e4r5t", "abcdefghij", "1111111111", "0000000000", "senhasenha", "password123",
    ];

    private static readonly string[] CoresDeAvatar =
        ["#e8c05a", "#9bcf8a", "#8fb7d9", "#e39a8a", "#c9a8e6", "#f0b983", "#8fd9cc", "#eaa6c6", "#c8d98a", "#c4c0b4"];

    public async Task<Resultado> CriarContaAsync(PedidoDeCadastro pedido, CancellationToken ct = default)
    {
        var erro = ValidarAceites(pedido.AceitouTermos, pedido.DeclarouMaioridade)
            ?? ValidarApelido(pedido.Apelido) ?? ValidarEmail(pedido.Email) ?? ValidarSenha(pedido.Senha);
        if (erro is not null)
            return Resultado.Falha(erro);

        var emailHash = criptografia.IndiceCego(pedido.Email);
        if (await repositorio.BuscarAsync<Jogador>(j => j.EmailHash == emailHash, ct) is not null)
            return Resultado.Falha(ChavesDeErro.EmailEmUso);
        if (await ApelidoEmUsoAsync(pedido.Apelido, ct))
            return Resultado.Falha(ChavesDeErro.ApelidoEmUso);

        var agora = relogio.AgoraUtc;
        var conta = NovaConta(pedido.Apelido, pedido.Email, pedido.Idioma, agora);
        conta.SenhaHash = hashDeSenha.Gerar(pedido.Senha);
        repositorio.Adicionar(conta);
        RegistrarAceites(conta.Id, pedido.Ip, agora);
        var token = NovoToken(conta.Id, TipoDeToken.ConfirmacaoDeEmail, agora.AddHours(PoliticaDeConta.HorasDoTokenDeEmail));
        Auditar(conta.Id, "ContaCriada", agora);
        await repositorio.SalvarAsync(ct);

        await enviadorDeEmail.EnviarAsync(
            pedido.Email,
            textos.Obter(Textos.Email_Confirmacao_Assunto, conta.Idioma),
            textos.Obter(Textos.Email_Confirmacao_Corpo, conta.Idioma, pedido.MontarLinkDeConfirmacao(token)),
            ct);
        return Resultado.Ok;
    }

    public async Task<Resultado> ConfirmarEmailAsync(string token, CancellationToken ct = default)
    {
        var achado = await BuscarTokenValidoAsync(token, TipoDeToken.ConfirmacaoDeEmail, ct);
        if (achado is null)
            return Resultado.Falha(ChavesDeErro.TokenInvalido);

        var agora = relogio.AgoraUtc;
        var conta = (await repositorio.BuscarAsync<Jogador>(j => j.Id == achado.JogadorId, ct))!;
        conta.EmailConfirmadoEm ??= agora;
        achado.UsadoEm = agora;
        await repositorio.SalvarAsync(ct);
        return Resultado.Ok;
    }

    public async Task<Resultado<SessaoDeConta>> EntrarAsync(string email, string senha, string ip, CancellationToken ct = default)
    {
        var hash = criptografia.IndiceCego(email);
        var agora = relogio.AgoraUtc;
        var conta = await repositorio.BuscarAsync<Jogador>(j => j.EmailHash == hash && j.RemovidoEm == null, ct);
        if (conta is null || conta.BloqueadoAte > agora)
            return Resultado<SessaoDeConta>.Falha(ChavesDeErro.CredenciaisInvalidas);

        if (conta.SenhaHash is null || !hashDeSenha.Conferir(conta.SenhaHash, senha))
        {
            conta.FalhasDeLogin++;
            if (conta.FalhasDeLogin >= PoliticaDeConta.FalhasParaBloquear)
            {
                conta.BloqueadoAte = agora.AddMinutes(PoliticaDeConta.MinutosDeBloqueio);
                conta.FalhasDeLogin = 0;
            }
            await repositorio.SalvarAsync(ct);
            return Resultado<SessaoDeConta>.Falha(ChavesDeErro.CredenciaisInvalidas);
        }

        conta.FalhasDeLogin = 0;
        conta.BloqueadoAte = null;
        await RegistrarEntradaAsync(conta, ip, agora, ct);
        return Resultado<SessaoDeConta>.Ok(Sessao(conta));
    }

    public async Task<ResultadoDoGoogle> EntrarComGoogleAsync(IdentidadeGoogle google, string ip, CancellationToken ct = default)
    {
        var idHash = criptografia.IndiceCego(google.Id);
        var login = await repositorio.BuscarAsync<LoginExterno>(
            l => l.Provedor == PoliticaDeConta.ProvedorGoogle && l.IdExternoHash == idHash, ct);
        if (login is null)
            return google.EmailVerificado
                ? ResultadoDoGoogle.Cadastro()
                : ResultadoDoGoogle.Falha(ChavesDeErro.GoogleEmailNaoVerificado);

        // O vínculo é apagado junto com a conta, então a chave estrangeira garante que a conta existe.
        var conta = (await repositorio.BuscarAsync<Jogador>(j => j.Id == login.JogadorId, ct))!;
        await RegistrarEntradaAsync(conta, ip, relogio.AgoraUtc, ct);
        return ResultadoDoGoogle.Entrou(Sessao(conta));
    }

    public async Task<Resultado<SessaoDeConta>> ConcluirCadastroComGoogleAsync(
        IdentidadeGoogle google, string apelido, bool aceitouTermos, bool declarouMaioridade,
        string? idioma, string ip, CancellationToken ct = default)
    {
        var erro = ValidarAceites(aceitouTermos, declarouMaioridade);
        if (erro is null && !google.EmailVerificado)
            erro = ChavesDeErro.GoogleEmailNaoVerificado;
        if (erro is not null)
            return Resultado<SessaoDeConta>.Falha(erro);

        var agora = relogio.AgoraUtc;
        var emailHash = criptografia.IndiceCego(google.Email);
        var conta = await repositorio.BuscarAsync<Jogador>(j => j.EmailHash == emailHash && j.RemovidoEm == null, ct);
        if (conta is null)
        {
            erro = ValidarApelido(apelido);
            if (erro is null && await ApelidoEmUsoAsync(apelido, ct))
                erro = ChavesDeErro.ApelidoEmUso;
            if (erro is not null)
                return Resultado<SessaoDeConta>.Falha(erro);

            conta = NovaConta(apelido, google.Email, idioma, agora);
            repositorio.Adicionar(conta);
            Auditar(conta.Id, "ContaCriadaComGoogle", agora);
        }

        conta.EmailConfirmadoEm ??= agora;
        repositorio.Adicionar(new LoginExterno
        {
            Id = Guid.NewGuid(),
            JogadorId = conta.Id,
            Provedor = PoliticaDeConta.ProvedorGoogle,
            IdExternoHash = criptografia.IndiceCego(google.Id),
            CriadoEm = agora,
        });
        RegistrarAceites(conta.Id, ip, agora);
        await RegistrarEntradaAsync(conta, ip, agora, ct);
        return Resultado<SessaoDeConta>.Ok(Sessao(conta));
    }

    /// <summary>Sempre responde igual, exista o e-mail ou não.</summary>
    public async Task EsqueciSenhaAsync(string email, Func<string, string> montarLink, CancellationToken ct = default)
    {
        var hash = criptografia.IndiceCego(email);
        var conta = await repositorio.BuscarAsync<Jogador>(j => j.EmailHash == hash && j.RemovidoEm == null, ct);
        if (conta is null)
            return;

        var agora = relogio.AgoraUtc;
        var token = NovoToken(conta.Id, TipoDeToken.RedefinicaoDeSenha, agora.AddHours(PoliticaDeConta.HorasDoTokenDeSenha));
        await repositorio.SalvarAsync(ct);
        await enviadorDeEmail.EnviarAsync(
            email,
            textos.Obter(Textos.Email_Redefinicao_Assunto, conta.Idioma),
            textos.Obter(Textos.Email_Redefinicao_Corpo, conta.Idioma, montarLink(token)),
            ct);
    }

    public async Task<Resultado> RedefinirSenhaAsync(string token, string novaSenha, CancellationToken ct = default)
    {
        var erro = ValidarSenha(novaSenha);
        if (erro is not null)
            return Resultado.Falha(erro);
        var achado = await BuscarTokenValidoAsync(token, TipoDeToken.RedefinicaoDeSenha, ct);
        if (achado is null)
            return Resultado.Falha(ChavesDeErro.TokenInvalido);

        var agora = relogio.AgoraUtc;
        var conta = (await repositorio.BuscarAsync<Jogador>(j => j.Id == achado.JogadorId, ct))!;
        conta.SenhaHash = hashDeSenha.Gerar(novaSenha);
        conta.CarimboDeSeguranca = Guid.NewGuid();
        conta.FalhasDeLogin = 0;
        conta.BloqueadoAte = null;
        achado.UsadoEm = agora;
        Auditar(conta.Id, "SenhaRedefinida", agora);
        await repositorio.SalvarAsync(ct);
        return Resultado.Ok;
    }

    /// <summary>Usada pelo cookie a cada requisição: a sessão cai se a conta sumiu ou a senha foi trocada.</summary>
    public async Task<bool> SessaoValidaAsync(Guid id, Guid carimbo, CancellationToken ct = default) =>
        await repositorio.BuscarAsync<Jogador>(j => j.Id == id && j.RemovidoEm == null && j.CarimboDeSeguranca == carimbo, ct) is not null;

    public async Task<SessaoDeConta?> ObterSessaoAsync(Guid id, CancellationToken ct = default)
    {
        var conta = await repositorio.BuscarAsync<Jogador>(j => j.Id == id && j.RemovidoEm == null, ct);
        return conta is null ? null : Sessao(conta);
    }

    public async Task<Resultado> AlterarIdiomaAsync(Guid id, string idioma, CancellationToken ct = default)
    {
        var conta = await repositorio.BuscarAsync<Jogador>(j => j.Id == id && j.RemovidoEm == null, ct);
        if (conta is null)
            return Resultado.Falha(ChavesDeErro.ContaNaoEncontrada);
        conta.Idioma = Idiomas.Normalizar(idioma);
        await repositorio.SalvarAsync(ct);
        return Resultado.Ok;
    }

    private static string? ValidarAceites(bool aceitouTermos, bool declarouMaioridade) =>
        !aceitouTermos ? ChavesDeErro.AceiteObrigatorio : !declarouMaioridade ? ChavesDeErro.MaioridadeObrigatoria : null;

    internal static string? ValidarApelido(string apelido) =>
        apelido.Trim().Length is >= PoliticaDeConta.ApelidoMinimo and <= PoliticaDeConta.ApelidoMaximo
            ? null
            : ChavesDeErro.ApelidoInvalido;

    internal static string? ValidarEmail(string email) =>
        new EmailAddressAttribute().IsValid(email) && email.Contains('.') ? null : ChavesDeErro.EmailInvalido;

    private static string? ValidarSenha(string senha) =>
        senha.Length < PoliticaDeConta.SenhaMinima || SenhasComuns.Contains(senha, StringComparer.OrdinalIgnoreCase)
            ? ChavesDeErro.SenhaFraca
            : null;

    private async Task<bool> ApelidoEmUsoAsync(string apelido, CancellationToken ct)
    {
        var hash = criptografia.IndiceCego(apelido);
        return await repositorio.BuscarAsync<Jogador>(j => j.ApelidoHash == hash, ct) is not null;
    }

    private Jogador NovaConta(string apelido, string email, string? idioma, DateTime agora)
    {
        var apelidoHash = criptografia.IndiceCego(apelido);
        return new Jogador
        {
            Id = Guid.NewGuid(),
            ApelidoCifrado = criptografia.Cifrar(apelido.Trim()),
            ApelidoHash = apelidoHash,
            EmailCifrado = criptografia.Cifrar(email.Trim()),
            EmailHash = criptografia.IndiceCego(email),
            Idioma = Idiomas.Normalizar(idioma),
            CorDoAvatar = CoresDeAvatar[apelidoHash[0] % CoresDeAvatar.Length],
            CriadoEm = agora,
        };
    }

    internal static string GerarToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private string NovoToken(Guid jogadorId, TipoDeToken tipo, DateTime expiraEm)
    {
        var token = GerarToken();
        repositorio.Adicionar(new TokenDeConta
        {
            Id = Guid.NewGuid(),
            JogadorId = jogadorId,
            Tipo = tipo,
            TokenHash = criptografia.HashDeToken(token),
            ExpiraEm = expiraEm,
        });
        return token;
    }

    private async Task<TokenDeConta?> BuscarTokenValidoAsync(string token, TipoDeToken tipo, CancellationToken ct)
    {
        var hash = criptografia.HashDeToken(token);
        var agora = relogio.AgoraUtc;
        return await repositorio.BuscarAsync<TokenDeConta>(
            t => t.TokenHash == hash && t.Tipo == tipo && t.UsadoEm == null && t.ExpiraEm > agora, ct);
    }

    private void RegistrarAceites(Guid jogadorId, string ip, DateTime agora)
    {
        var ipHash = criptografia.IndiceCego(ip);
        foreach (var documento in new[] { PoliticaDeConta.DocumentoTermos, PoliticaDeConta.DocumentoPrivacidade })
        {
            repositorio.Adicionar(new Consentimento
            {
                Id = Guid.NewGuid(),
                JogadorId = jogadorId,
                Documento = documento,
                Versao = PoliticaDeConta.VersaoDosDocumentos,
                AceitoEm = agora,
                IpHash = ipHash,
            });
        }
    }

    private async Task RegistrarEntradaAsync(Jogador conta, string ip, DateTime agora, CancellationToken ct)
    {
        conta.UltimoAcessoEm = agora;
        repositorio.Adicionar(new RegistroDeAcesso
        {
            Id = Guid.NewGuid(),
            JogadorId = conta.Id,
            IpCifrado = criptografia.Cifrar(ip),
            Tipo = TipoDeAcesso.Login,
            OcorreuEm = agora,
        });
        await repositorio.SalvarAsync(ct);
    }

    private void Auditar(Guid jogadorId, string acao, DateTime agora) =>
        repositorio.Adicionar(new RegistroDeAuditoria { Id = Guid.NewGuid(), JogadorId = jogadorId, Acao = acao, OcorreuEm = agora });

    private SessaoDeConta Sessao(Jogador conta) =>
        new(conta.Id, criptografia.Decifrar(conta.ApelidoCifrado!), conta.Idioma, conta.EmailConfirmadoEm is not null, conta.CarimboDeSeguranca, conta.CorDoAvatar);
}
