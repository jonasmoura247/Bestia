using System.Text.Json;
using System.Threading.Channels;
using Bestia.Aplicacao.Modelos;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using NUnit.Framework;

namespace Bestia.Testes.Web;

public class HubWebTestes
{
    private sealed class Escuta
    {
        public Channel<JsonElement> Visoes { get; } = Channel.CreateUnbounded<JsonElement>();
        public Channel<string> Erros { get; } = Channel.CreateUnbounded<string>();
        public Channel<(int Assento, int Emote)> Emotes { get; } = Channel.CreateUnbounded<(int, int)>();
        public Channel<bool> Substituida { get; } = Channel.CreateUnbounded<bool>();
        public TaskCompletionSource Fechada { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static (HubConnection Conexao, Escuta Escuta) Montar(BestiaFactory fabrica, ClienteDeTeste cliente)
    {
        var conexao = new HubConnectionBuilder()
            .WithUrl(new Uri(fabrica.Server.BaseAddress, "/hub/mesa"), opcoes =>
            {
                opcoes.HttpMessageHandlerFactory = _ => fabrica.Server.CreateHandler();
                opcoes.Transports = HttpTransportType.LongPolling;
                opcoes.Headers["Cookie"] = cliente.CookieHeader;
            })
            .Build();
        var escuta = new Escuta();
        conexao.On<JsonElement>("MesaAtualizada", v => escuta.Visoes.Writer.TryWrite(v));
        conexao.On<string>("Erro", e => escuta.Erros.Writer.TryWrite(e));
        conexao.On<int, int>("EmoteRecebido", (a, e) => escuta.Emotes.Writer.TryWrite((a, e)));
        conexao.On("SessaoSubstituida", () => escuta.Substituida.Writer.TryWrite(true));
        conexao.Closed += _ =>
        {
            escuta.Fechada.TrySetResult();
            return Task.CompletedTask;
        };
        return (conexao, escuta);
    }

    private static async Task<T> EsperarAsync<T>(Channel<T> canal, Func<T, bool>? aceitar = null)
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var item = await canal.Reader.ReadAsync(limite.Token);
            if (aceitar is null || aceitar(item))
                return item;
        }
    }

    private static async Task<ClienteDeTeste> LogadoAsync(BestiaFactory fabrica, string apelido, string email)
    {
        var cliente = fabrica.NovoCliente();
        await cliente.CadastrarAsync(apelido, email);
        foreach (var conta in fabrica.Repositorio.Todos<Jogador>())
            conta.EmailConfirmadoEm ??= fabrica.Relogio.AgoraUtc;
        await cliente.EntrarAsync(email);
        return cliente;
    }

    [Test]
    public async Task Deve_RecusarConexaoSemLogin()
    {
        await using var fabrica = new BestiaFactory();
        var (conexao, _) = Montar(fabrica, fabrica.NovoCliente());

        Assert.CatchAsync(() => conexao.StartAsync());
        await conexao.DisposeAsync();
    }

    [Test]
    public async Task Deve_JogarUmaMao_PeloHub_ComVisaoFiltrada()
    {
        await using var fabrica = new BestiaFactory();
        var ana = await LogadoAsync(fabrica, "Ana", "ana@exemplo.com");
        var criar = await ana.PostAsync("/salas/criar", new() { ["Nome"] = "Mesa do canto", ["Maximo"] = "5" }, "/salas");
        var (conexao, escuta) = Montar(fabrica, ana);
        await conexao.StartAsync();

        var espera = await EsperarAsync(escuta.Visoes);
        await conexao.InvokeAsync("PreencherComBots", 2);
        var comBots = await EsperarAsync(escuta.Visoes, v => v.GetProperty("assentos").GetArrayLength() == 3);
        await conexao.InvokeAsync("IniciarPartida");
        var partida = await EsperarAsync(escuta.Visoes, v => v.GetProperty("fase").GetString() == "partida");
        await conexao.InvokeAsync("EnviarEmote", 1);
        var emote = await EsperarAsync(escuta.Emotes);

        Assert.That(espera.GetProperty("fase").GetString(), Is.EqualTo("espera"));
        Assert.That(espera.GetProperty("souCriador").GetBoolean(), Is.True);
        Assert.That(comBots.GetProperty("bots").GetInt32(), Is.EqualTo(2));
        Assert.That(partida.GetProperty("minhaMao").GetArrayLength(), Is.EqualTo(3));
        Assert.That(partida.GetProperty("baus").EnumerateArray().Select(b => b.EnumerateObject().Count()), Is.All.EqualTo(3));
        Assert.That(emote.Emote, Is.EqualTo(1));

        await conexao.DisposeAsync();
        Assert.That(criar.Local(), Does.StartWith("/salas/"));
    }

    [Test]
    public async Task Deve_AvisarErro_QuandoAcaoNaoEPermitida()
    {
        await using var fabrica = new BestiaFactory();
        var ana = await LogadoAsync(fabrica, "Ana", "ana@exemplo.com");
        await ana.PostAsync("/salas/criar", new() { ["Nome"] = "Mesa do canto", ["Maximo"] = "5" }, "/salas");
        var (conexao, escuta) = Montar(fabrica, ana);
        await conexao.StartAsync();

        await conexao.InvokeAsync("PegarBau", 0);
        var bau = await EsperarAsync(escuta.Erros);
        await conexao.InvokeAsync("JogarCarta", 0, 11);
        var carta = await EsperarAsync(escuta.Erros);
        await conexao.InvokeAsync("Decidir", true);
        var erro = await EsperarAsync(escuta.Erros);
        await conexao.InvokeAsync("IniciarPartida");
        var poucos = await EsperarAsync(escuta.Erros);

        Assert.That(bau, Is.EqualTo("Erro_AcaoForaDeHora"));
        Assert.That(carta, Is.EqualTo("Erro_AcaoForaDeHora"));
        Assert.That(erro, Is.EqualTo("Erro_AcaoForaDeHora"));
        Assert.That(poucos, Is.EqualTo("Erro_JogadoresInsuficientes"));
        await conexao.DisposeAsync();
    }

    [Test]
    public async Task Deve_EntrarNaSalaPeloHub_ESairDela()
    {
        await using var fabrica = new BestiaFactory();
        var ana = await LogadoAsync(fabrica, "Ana", "ana@exemplo.com");
        var criar = await ana.PostAsync("/salas/criar", new() { ["Nome"] = "Mesa do canto", ["Maximo"] = "5" }, "/salas");
        var codigo = criar.Local()[7..];
        var bia = await LogadoAsync(fabrica, "Bia", "bia@exemplo.com");
        var (daAna, escutaAna) = Montar(fabrica, ana);
        var (daBia, escutaBia) = Montar(fabrica, bia);
        await daAna.StartAsync();
        await EsperarAsync(escutaAna.Visoes);
        await daBia.StartAsync();

        await daBia.InvokeAsync("EntrarNaSala", codigo);
        var comDois = await EsperarAsync(escutaAna.Visoes, v => v.GetProperty("assentos").GetArrayLength() == 2);
        await daBia.InvokeAsync("Desistir");
        var comUm = await EsperarAsync(escutaAna.Visoes, v => v.GetProperty("assentos").GetArrayLength() == 1);
        await daBia.InvokeAsync("EntrarNaSala", "ZZZZZZ");
        var erro = await EsperarAsync(escutaBia.Erros);
        await daBia.InvokeAsync("SairDaSala");
        var erroSair = await EsperarAsync(escutaBia.Erros);

        Assert.That(comDois.GetProperty("assentos")[1].GetProperty("nome").GetString(), Is.EqualTo("Bia"));
        Assert.That(comUm.GetProperty("assentos").GetArrayLength(), Is.EqualTo(1));
        Assert.That(erro, Is.EqualTo("Erro_SalaNaoEncontrada"));
        Assert.That(erroSair, Is.EqualTo("Erro_NaoEstaEmSala"));
        await daAna.DisposeAsync();
        await daBia.DisposeAsync();
    }

    [Test]
    public async Task Deve_DerrubarAPrimeiraAba_QuandoASegundaConecta()
    {
        await using var fabrica = new BestiaFactory();
        var ana = await LogadoAsync(fabrica, "Ana", "ana@exemplo.com");
        var (primeira, escutaPrimeira) = Montar(fabrica, ana);
        var (segunda, _) = Montar(fabrica, ana);
        await primeira.StartAsync();

        await segunda.StartAsync();
        var aviso = await EsperarAsync(escutaPrimeira.Substituida);

        Assert.That(aviso, Is.True);
        await primeira.DisposeAsync();
        await segunda.DisposeAsync();
    }

    [Test]
    public async Task Deve_EncerrarAConexao_QuandoPassaDoLimiteDeMensagens()
    {
        await using var fabrica = new BestiaFactory();
        var ana = await LogadoAsync(fabrica, "Ana", "ana@exemplo.com");
        await ana.PostAsync("/salas/criar", new() { ["Nome"] = "Mesa do canto", ["Maximo"] = "5" }, "/salas");
        var (conexao, escuta) = Montar(fabrica, ana);
        await conexao.StartAsync();

        for (var i = 0; i < 60 && !escuta.Fechada.Task.IsCompleted; i++)
            await conexao.SendAsync("EnviarEmote", 0);

        var fechou = await Task.WhenAny(escuta.Fechada.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.That(fechou, Is.SameAs(escuta.Fechada.Task));
        await conexao.DisposeAsync();
    }
}
