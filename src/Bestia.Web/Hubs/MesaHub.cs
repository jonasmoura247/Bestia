using System.Collections.Concurrent;
using Bestia.Aplicacao.Contas;
using Bestia.Aplicacao.Partidas;
using Bestia.Dominio;
using Bestia.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Bestia.Web.Hubs;

/// <summary>Uma conexão por conta: a mais nova derruba a anterior ("segunda aba derruba a primeira").</summary>
public sealed class ControleDeConexoes
{
    private readonly ConcurrentDictionary<Guid, string> _porConta = new();

    /// <summary>Registra a conexão e devolve a que ficou para trás, se havia.</summary>
    public string? Registrar(Guid conta, string conexao)
    {
        string? anterior = null;
        _porConta.AddOrUpdate(conta, conexao, (_, atual) =>
        {
            anterior = atual;
            return conexao;
        });
        return anterior;
    }

    public void Remover(Guid conta, string conexao) =>
        _porConta.TryRemove(new KeyValuePair<Guid, string>(conta, conexao));
}

/// <summary>No máximo 20 mensagens por segundo por conexão.</summary>
public sealed class LimitadorDeMensagens(TimeProvider? relogio = null)
{
    public const int MaximoPorSegundo = 20;

    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, (long Segundo, int Contagem)> _janelas = new();

    public bool Permitir(string conexao)
    {
        var segundo = _relogio.GetUtcNow().ToUnixTimeSeconds();
        var janela = _janelas.AddOrUpdate(
            conexao,
            (segundo, 1),
            (_, atual) => atual.Segundo == segundo ? (segundo, atual.Contagem + 1) : (segundo, 1));
        return janela.Contagem <= MaximoPorSegundo;
    }

    public void Esquecer(string conexao) => _janelas.TryRemove(conexao, out _);
}

public sealed class NotificadorSignalR(IHubContext<MesaHub> hub) : INotificadorDaMesa
{
    public Task MesaAtualizadaAsync(Guid contaId, VisaoDaMesa visao) =>
        hub.Clients.User(contaId.ToString()).SendAsync("MesaAtualizada", visao);

    public Task EmoteAsync(Guid contaId, int assento, int emote) =>
        hub.Clients.User(contaId.ToString()).SendAsync("EmoteRecebido", assento, emote);
}

[Authorize]
public sealed class MesaHub(
    MesaServico mesas, ContaServico contas, ControleDeConexoes conexoes, LimitadorDeMensagens limitador) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var conta = Context.User!.ContaId();
        var anterior = conexoes.Registrar(conta, Context.ConnectionId);
        if (anterior is not null)
            await Clients.Client(anterior).SendAsync("SessaoSubstituida");

        var visao = mesas.ObterVisao(conta);
        if (visao is not null)
            await Clients.Caller.SendAsync("MesaAtualizada", visao);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? excecao)
    {
        conexoes.Remover(Context.User!.ContaId(), Context.ConnectionId);
        limitador.Esquecer(Context.ConnectionId);
        return base.OnDisconnectedAsync(excecao);
    }

    public Task EntrarNaSala(string codigo) =>
        Executar(async conta =>
        {
            var sessao = await contas.ObterSessaoAsync(conta);
            var entrada = await mesas.EntrarAsync(sessao!.ComoPerfil(), codigo);
            return entrada.Sucesso ? Resultado.Ok : Resultado.Falha(entrada.ChaveDoErro!);
        });

    public Task SairDaSala() => Executar(conta => mesas.SairAsync(conta));

    public Task Desistir() => Executar(conta => mesas.SairAsync(conta));

    public Task PreencherComBots(int quantidade) => Executar(conta => mesas.PreencherComBotsAsync(conta, quantidade));

    public Task IniciarPartida() => Executar(conta => mesas.IniciarPartidaAsync(conta));

    public Task Decidir(bool ficar) => Executar(conta => mesas.DecidirAsync(conta, ficar));

    public Task PegarBau(int numero) => Executar(conta => mesas.PegarBauAsync(conta, numero));

    public Task JogarCarta(int naipe, int valor) => Executar(conta => mesas.JogarCartaAsync(conta, naipe, valor));

    public Task EnviarEmote(int tipo) => Executar(conta => mesas.EnviarEmoteAsync(conta, tipo));

    private async Task Executar(Func<Guid, Task<Resultado>> acao)
    {
        if (!limitador.Permitir(Context.ConnectionId))
        {
            Context.Abort();
            return;
        }

        var resultado = await acao(Context.User!.ContaId());
        if (!resultado.Sucesso)
            await Clients.Caller.SendAsync("Erro", resultado.ChaveDoErro);
    }
}
