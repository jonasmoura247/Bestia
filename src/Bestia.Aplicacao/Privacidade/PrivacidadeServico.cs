using System.Text.Json;
using Bestia.Aplicacao.Contas;
using Bestia.Aplicacao.Contratos;
using Bestia.Aplicacao.Modelos;
using Bestia.Dominio;
using Bestia.Recursos;

namespace Bestia.Aplicacao.Privacidade;

public sealed record ConsentimentoVisao(string Documento, string Versao, DateTime AceitoEm, DateTime? RevogadoEm);

public sealed record MeusDados(
    string Apelido, string Email, string Idioma, DateTime CriadoEm, DateTime? EmailConfirmadoEm,
    DateTime? UltimoAcessoEm, bool TemSenha, IReadOnlyList<string> Logins,
    IReadOnlyList<ConsentimentoVisao> Consentimentos, int Partidas);

public static class PoliticaDeRetencao
{
    public const int DiasParaResponder = 15;
    public const int MesesDeRegistroDeAcesso = 6;
    public const int MesesDeInatividade = 24;
    public const int DiasDeAvisoAntesDeExcluir = 30;
    public const string AcaoAvisoDeInatividade = "AvisoDeInatividade";
}

public sealed class PrivacidadeServico(
    IRepositorio repositorio, ICriptografia criptografia, IHashDeSenha hashDeSenha,
    IRelogio relogio, IEnviadorDeEmail enviadorDeEmail, ITextos textos)
{
    public async Task<MeusDados?> MeusDadosAsync(Guid contaId, CancellationToken ct = default)
    {
        var conta = await ContaAsync(contaId, ct);
        if (conta is null)
            return null;

        var logins = await repositorio.ListarAsync<LoginExterno>(l => l.JogadorId == contaId, ct);
        var consentimentos = await repositorio.ListarAsync<Consentimento>(c => c.JogadorId == contaId, ct);
        var partidas = await repositorio.ListarAsync<PartidaJogador>(p => p.JogadorId == contaId, ct);
        return new MeusDados(
            criptografia.Decifrar(conta.ApelidoCifrado!), criptografia.Decifrar(conta.EmailCifrado!), conta.Idioma,
            conta.CriadoEm, conta.EmailConfirmadoEm, conta.UltimoAcessoEm, conta.SenhaHash is not null,
            logins.Select(l => l.Provedor).ToList(),
            consentimentos.OrderBy(c => c.AceitoEm).Select(c => new ConsentimentoVisao(c.Documento, c.Versao, c.AceitoEm, c.RevogadoEm)).ToList(),
            partidas.Count);
    }

    /// <summary>Portabilidade: tudo que guardamos, em JSON.</summary>
    public async Task<Resultado<string>> ExportarAsync(Guid contaId, CancellationToken ct = default)
    {
        var dados = await MeusDadosAsync(contaId, ct);
        if (dados is null)
            return Resultado<string>.Falha(ChavesDeErro.ContaNaoEncontrada);

        var partidas = await repositorio.ListarAsync<PartidaJogador>(p => p.JogadorId == contaId, ct);
        var solicitacoes = await repositorio.ListarAsync<SolicitacaoPrivacidade>(s => s.JogadorId == contaId, ct);
        var agora = relogio.AgoraUtc;
        RegistrarSolicitacao(contaId, TipoDeSolicitacao.Portabilidade, agora, respondida: true);
        Auditar(contaId, "DadosExportados", agora);
        await repositorio.SalvarAsync(ct);

        var pacote = new
        {
            GeradoEm = agora,
            Dados = dados,
            Partidas = partidas.Select(p => new { p.PartidaId, p.Assento, p.FichasIniciais, p.FichasFinais, p.Colocacao }),
            Solicitacoes = solicitacoes.Select(s => new { s.Tipo, s.CriadaEm, s.RespondidaEm, s.Situacao }),
        };
        return Resultado<string>.Ok(JsonSerializer.Serialize(pacote, new JsonSerializerOptions { WriteIndented = true }));
    }

    public async Task<Resultado> AlterarApelidoAsync(Guid contaId, string apelido, CancellationToken ct = default)
    {
        var conta = await ContaAsync(contaId, ct);
        if (conta is null)
            return Resultado.Falha(ChavesDeErro.ContaNaoEncontrada);
        var erro = ContaServico.ValidarApelido(apelido);
        if (erro is not null)
            return Resultado.Falha(erro);

        var hash = criptografia.IndiceCego(apelido);
        if (await repositorio.BuscarAsync<Jogador>(j => j.ApelidoHash == hash && j.Id != contaId, ct) is not null)
            return Resultado.Falha(ChavesDeErro.ApelidoEmUso);

        conta.ApelidoCifrado = criptografia.Cifrar(apelido.Trim());
        conta.ApelidoHash = hash;
        Auditar(contaId, "ApelidoAlterado", relogio.AgoraUtc);
        await repositorio.SalvarAsync(ct);
        return Resultado.Ok;
    }

    /// <summary>E-mail novo exige a senha e nova confirmação por link.</summary>
    public async Task<Resultado> AlterarEmailAsync(
        Guid contaId, string novoEmail, string senha, Func<string, string> montarLink, CancellationToken ct = default)
    {
        var conta = await ContaAsync(contaId, ct);
        if (conta is null)
            return Resultado.Falha(ChavesDeErro.ContaNaoEncontrada);
        if (conta.SenhaHash is null)
            return Resultado.Falha(ChavesDeErro.OperacaoRequerSenha);
        if (!hashDeSenha.Conferir(conta.SenhaHash, senha))
            return Resultado.Falha(ChavesDeErro.SenhaIncorreta);
        var erro = ContaServico.ValidarEmail(novoEmail);
        if (erro is not null)
            return Resultado.Falha(erro);

        var hash = criptografia.IndiceCego(novoEmail);
        if (await repositorio.BuscarAsync<Jogador>(j => j.EmailHash == hash && j.Id != contaId, ct) is not null)
            return Resultado.Falha(ChavesDeErro.EmailEmUso);

        var agora = relogio.AgoraUtc;
        conta.EmailCifrado = criptografia.Cifrar(novoEmail.Trim());
        conta.EmailHash = hash;
        conta.EmailConfirmadoEm = null;
        var token = ContaServico.GerarToken();
        repositorio.Adicionar(new TokenDeConta
        {
            Id = Guid.NewGuid(),
            JogadorId = contaId,
            Tipo = TipoDeToken.ConfirmacaoDeEmail,
            TokenHash = criptografia.HashDeToken(token),
            ExpiraEm = agora.AddHours(PoliticaDeConta.HorasDoTokenDeEmail),
        });
        Auditar(contaId, "EmailAlterado", agora);
        await repositorio.SalvarAsync(ct);

        await enviadorDeEmail.EnviarAsync(
            novoEmail,
            textos.Obter(Textos.Email_Confirmacao_Assunto, conta.Idioma),
            textos.Obter(Textos.Email_Confirmacao_Corpo, conta.Idioma, montarLink(token)),
            ct);
        return Resultado.Ok;
    }

    public async Task<Resultado> SolicitarAoEncarregadoAsync(Guid contaId, TipoDeSolicitacao tipo, CancellationToken ct = default)
    {
        if (await ContaAsync(contaId, ct) is null)
            return Resultado.Falha(ChavesDeErro.ContaNaoEncontrada);
        RegistrarSolicitacao(contaId, tipo, relogio.AgoraUtc, respondida: false);
        await repositorio.SalvarAsync(ct);
        return Resultado.Ok;
    }

    /// <summary>Documentos essenciais: retirar o consentimento encerra a conta.</summary>
    public async Task<Resultado> RevogarConsentimentoAsync(Guid contaId, string documento, CancellationToken ct = default)
    {
        var conta = await ContaAsync(contaId, ct);
        if (conta is null)
            return Resultado.Falha(ChavesDeErro.ContaNaoEncontrada);

        var agora = relogio.AgoraUtc;
        var vigentes = await repositorio.ListarAsync<Consentimento>(c => c.JogadorId == contaId && c.Documento == documento && c.RevogadoEm == null, ct);
        foreach (var consentimento in vigentes)
            consentimento.RevogadoEm = agora;
        RegistrarSolicitacao(contaId, TipoDeSolicitacao.Revogacao, agora, respondida: true);
        await AnonimizarAsync(conta, agora, ct);
        await repositorio.SalvarAsync(ct);
        return Resultado.Ok;
    }

    /// <summary>Pede a senha; para quem só entra com o Google, a nova autenticação é feita antes, pela Web.</summary>
    public async Task<Resultado> ExcluirContaAsync(Guid contaId, string? senha, bool reautenticouComGoogle, CancellationToken ct = default)
    {
        var conta = await ContaAsync(contaId, ct);
        if (conta is null)
            return Resultado.Falha(ChavesDeErro.ContaNaoEncontrada);

        var confirmou = conta.SenhaHash is null
            ? reautenticouComGoogle
            : senha is not null && hashDeSenha.Conferir(conta.SenhaHash, senha);
        if (!confirmou)
            return Resultado.Falha(ChavesDeErro.SenhaIncorreta);

        var agora = relogio.AgoraUtc;
        RegistrarSolicitacao(contaId, TipoDeSolicitacao.Eliminacao, agora, respondida: true);
        await AnonimizarAsync(conta, agora, ct);
        await repositorio.SalvarAsync(ct);
        return Resultado.Ok;
    }

    /// <summary>Confere se o identificador do Google é o vinculado a esta conta (reautenticação para excluir).</summary>
    public async Task<bool> ConfirmarIdentidadeGoogleAsync(Guid contaId, string idGoogle, CancellationToken ct = default)
    {
        var hash = criptografia.IndiceCego(idGoogle);
        return await repositorio.BuscarAsync<LoginExterno>(
            l => l.JogadorId == contaId && l.Provedor == PoliticaDeConta.ProvedorGoogle && l.IdExternoHash == hash, ct) is not null;
    }

    /// <summary>Rotina diária: registros de acesso, tokens vencidos e contas inativas.</summary>
    public async Task ExecutarRotinasAsync(CancellationToken ct = default)
    {
        var agora = relogio.AgoraUtc;

        var limiteDeAcessos = agora.AddMonths(-PoliticaDeRetencao.MesesDeRegistroDeAcesso);
        foreach (var acesso in await repositorio.ListarAsync<RegistroDeAcesso>(a => a.OcorreuEm < limiteDeAcessos, ct))
            repositorio.Remover(acesso);

        var limiteDeTokens = agora.AddDays(-1);
        foreach (var token in await repositorio.ListarAsync<TokenDeConta>(t => t.ExpiraEm < limiteDeTokens, ct))
            repositorio.Remover(token);

        var limiteDeInatividade = agora.AddMonths(-PoliticaDeRetencao.MesesDeInatividade);
        var inativas = await repositorio.ListarAsync<Jogador>(
            j => j.RemovidoEm == null && (j.UltimoAcessoEm ?? j.CriadoEm) < limiteDeInatividade, ct);
        foreach (var conta in inativas)
            await TratarInativaAsync(conta, agora, ct);

        await repositorio.SalvarAsync(ct);
    }

    private async Task TratarInativaAsync(Jogador conta, DateTime agora, CancellationToken ct)
    {
        var aviso = await repositorio.BuscarAsync<RegistroDeAuditoria>(
            r => r.JogadorId == conta.Id && r.Acao == PoliticaDeRetencao.AcaoAvisoDeInatividade, ct);
        if (aviso is null)
        {
            Auditar(conta.Id, PoliticaDeRetencao.AcaoAvisoDeInatividade, agora);
            await enviadorDeEmail.EnviarAsync(
                criptografia.Decifrar(conta.EmailCifrado!),
                textos.Obter(Textos.Email_Inatividade_Assunto, conta.Idioma),
                textos.Obter(Textos.Email_Inatividade_Corpo, conta.Idioma, PoliticaDeRetencao.DiasDeAvisoAntesDeExcluir),
                ct);
        }
        else if (aviso.OcorreuEm.AddDays(PoliticaDeRetencao.DiasDeAvisoAntesDeExcluir) <= agora)
        {
            await AnonimizarAsync(conta, agora, ct);
        }
    }

    /// <summary>Apaga o que identifica a pessoa e mantém a linha, para o histórico dos outros jogadores não quebrar.</summary>
    private async Task AnonimizarAsync(Jogador conta, DateTime agora, CancellationToken ct)
    {
        conta.ApelidoCifrado = null;
        conta.ApelidoHash = null;
        conta.EmailCifrado = null;
        conta.EmailHash = null;
        conta.SenhaHash = null;
        conta.RemovidoEm = agora;
        conta.CarimboDeSeguranca = Guid.NewGuid();

        foreach (var login in await repositorio.ListarAsync<LoginExterno>(l => l.JogadorId == conta.Id, ct))
            repositorio.Remover(login);
        foreach (var token in await repositorio.ListarAsync<TokenDeConta>(t => t.JogadorId == conta.Id, ct))
            repositorio.Remover(token);
        foreach (var acesso in await repositorio.ListarAsync<RegistroDeAcesso>(a => a.JogadorId == conta.Id, ct))
            repositorio.Remover(acesso);
        Auditar(conta.Id, "ContaExcluida", agora);
    }

    private Task<Jogador?> ContaAsync(Guid contaId, CancellationToken ct) =>
        repositorio.BuscarAsync<Jogador>(j => j.Id == contaId && j.RemovidoEm == null, ct);

    private void RegistrarSolicitacao(Guid contaId, TipoDeSolicitacao tipo, DateTime agora, bool respondida)
    {
        repositorio.Adicionar(new SolicitacaoPrivacidade
        {
            Id = Guid.NewGuid(),
            JogadorId = contaId,
            Tipo = tipo,
            CriadaEm = agora,
            PrazoEm = agora.AddDays(PoliticaDeRetencao.DiasParaResponder),
            RespondidaEm = respondida ? agora : null,
            Situacao = respondida ? SituacaoDaSolicitacao.Respondida : SituacaoDaSolicitacao.Aberta,
        });
    }

    private void Auditar(Guid contaId, string acao, DateTime agora) =>
        repositorio.Adicionar(new RegistroDeAuditoria { Id = Guid.NewGuid(), JogadorId = contaId, Acao = acao, OcorreuEm = agora });
}
