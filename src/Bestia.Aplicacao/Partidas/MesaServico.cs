using Bestia.Aplicacao.Contratos;
using Bestia.Dominio;
using Bestia.Dominio.Bots;
using Bestia.Dominio.Cartas;
using Bestia.Dominio.Jogadores;
using Bestia.Dominio.Partidas;
using Ent = Bestia.Aplicacao.Modelos;

namespace Bestia.Aplicacao.Partidas;

internal sealed class Participante
{
    /// <summary>Conta a quem o participante pertence; fixa, para o histórico.</summary>
    public Guid? ContaOriginal { get; init; }

    /// <summary>Conta conectada à mesa; vira null quando a pessoa sai.</summary>
    public Guid? ContaId { get; set; }

    public string Apelido { get; init; } = "";
    public string Cor { get; init; } = "";
    public bool EhBot { get; init; }
    public Jogador? Jogador { get; set; }
    public bool Conectado => ContaId is not null;
}

internal sealed class Mesa
{
    public Guid SalaId { get; init; }
    public string Codigo { get; init; } = "";
    public string Nome { get; init; } = "";
    public int Maximo { get; init; }
    public bool Privada { get; init; }
    public Guid CriadorId { get; set; }
    public List<Participante> Participantes { get; } = [];
    public List<Participante> Assentos { get; } = [];
    public FaseDaSala Fase { get; set; } = FaseDaSala.Espera;
    public Partida? Partida { get; set; }
    public Mao? Mao { get; set; }
    public Guid PartidaId { get; set; }
    public int NumeroDaMao { get; set; }
    public int Geracao { get; set; }
    public bool EmPausa { get; set; }
    public Task Tarefa { get; set; } = Task.CompletedTask;
    public int RodadasVistas { get; set; }
    public RodadaDisputada? RodadaEmExibicao { get; set; }
    public List<EventoVisao> Diario { get; } = [];
    public EventoVisao? Aviso { get; set; }
    public DateTime? PrazoFim { get; set; }
    public int PrazoSegundos { get; set; }
    public Dictionary<Guid, DateTime> UltimoEmote { get; } = [];
    public SemaphoreSlim Trava { get; } = new(1, 1);
    public Exception? UltimoErro { get; set; }
}

public sealed class MesaServico(IUnidadeDeTrabalho trabalho, IRelogio relogio, ISorteador sorteador, INotificadorDaMesa notificador)
{
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int TamanhoDoCodigo = 6;
    private const int EmotesDisponiveis = 5;
    private const int MaximoDoDiario = 60;
    private static readonly TimeSpan IntervaloDeEmote = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan PausaDaRodada = TimeSpan.FromSeconds(2.1);
    private static readonly TimeSpan PausaDaMao = TimeSpan.FromSeconds(2.4);
    private static readonly int[] EmotesDeVitoria = [1, 1, 4];
    private static readonly int[] EmotesDeDerrota = [3, 2, 0, 3];
    private static readonly string[] NomesDeBots = ["Ana", "Bia", "Caio", "Duda", "Edu", "Fê", "Gabi", "Hugo", "Iara", "Jota"];
    private static readonly string[] CoresDeBots = ["#9bcf8a", "#8fb7d9", "#e39a8a", "#c9a8e6", "#f0b983", "#8fd9cc", "#eaa6c6", "#c8d98a", "#c4c0b4", "#e8c05a"];
    private static readonly string[] Simbolos = ["♥", "♦", "♠", "♣"];
    private static readonly string[] Rotulos = ["4", "5", "6", "7", "8", "9", "J", "Q", "K", "2", "3", "A"];

    private readonly EstrategiaDeBot _bots = new(sorteador);
    private readonly Dictionary<string, Mesa> _porCodigo = [];
    private readonly Dictionary<Guid, Mesa> _porConta = [];
    private readonly object _registro = new();

    // ---------- consulta ----------

    public IReadOnlyList<ResumoDaSala> ListarSalasPublicas()
    {
        lock (_registro)
        {
            return _porCodigo.Values
                .Where(m => !m.Privada && m.Fase != FaseDaSala.Encerrada)
                .OrderBy(m => m.Nome)
                .Select(m => new ResumoDaSala(m.Codigo, m.Nome, m.Participantes.Count, m.Maximo, m.Fase == FaseDaSala.EmPartida))
                .ToList();
        }
    }

    public string? CodigoDaSalaDe(Guid contaId)
    {
        lock (_registro)
            return _porConta.TryGetValue(contaId, out var mesa) ? mesa.Codigo : null;
    }

    public VisaoDaMesa? ObterVisao(Guid contaId)
    {
        Mesa? mesa;
        lock (_registro)
            _porConta.TryGetValue(contaId, out mesa);
        return mesa is null ? null : Visao(mesa, contaId);
    }

    /// <summary>Para testes e para esperar o fim de uma ação agendada.</summary>
    public Task TarefaAgendadaDe(string codigo)
    {
        lock (_registro)
            return _porCodigo.TryGetValue(codigo, out var mesa) ? mesa.Tarefa : Task.CompletedTask;
    }

    public Exception? UltimoErroDe(string codigo)
    {
        lock (_registro)
            return _porCodigo.TryGetValue(codigo, out var mesa) ? mesa.UltimoErro : null;
    }

    // ---------- sala de espera ----------

    public async Task<Resultado<string>> CriarSalaAsync(PerfilDoJogador perfil, string nome, int maximo, bool privada, CancellationToken ct = default)
    {
        nome = nome.Trim();
        if (!perfil.EmailConfirmado)
            return Resultado<string>.Falha(ChavesDeErro.EmailNaoConfirmado);
        if (nome.Length is < 3 or > 30 || maximo is < ConfiguracaoDoJogo.MinimoDeJogadores or > ConfiguracaoDoJogo.MaximoDeJogadores)
            return Resultado<string>.Falha(ChavesDeErro.SalaInvalida);

        Mesa mesa;
        lock (_registro)
        {
            if (_porConta.ContainsKey(perfil.ContaId))
                return Resultado<string>.Falha(ChavesDeErro.JaEstaEmSala);
            mesa = new Mesa { SalaId = Guid.NewGuid(), Codigo = NovoCodigo(), Nome = nome, Maximo = maximo, Privada = privada, CriadorId = perfil.ContaId };
            mesa.Participantes.Add(Humano(perfil));
            _porCodigo[mesa.Codigo] = mesa;
            _porConta[perfil.ContaId] = mesa;
        }

        var agora = relogio.AgoraUtc;
        await trabalho.ExecutarAsync(repositorio =>
        {
            repositorio.Adicionar(new Ent.Sala
            {
                Id = mesa.SalaId,
                Nome = mesa.Nome,
                Codigo = mesa.Codigo,
                CriadaPorJogadorId = perfil.ContaId,
                MaximoDeJogadores = maximo,
                EhPrivada = privada,
                Situacao = Ent.SituacaoDaSala.Aberta,
                CriadaEm = agora,
            });
            repositorio.Adicionar(new Ent.SalaJogador { SalaId = mesa.SalaId, JogadorId = perfil.ContaId, EntrouEm = agora });
            return repositorio.SalvarAsync(ct);
        }, ct);

        await TransmitirAsync(mesa);
        return Resultado<string>.Ok(mesa.Codigo);
    }

    public async Task<Resultado<string>> EntrarAsync(PerfilDoJogador perfil, string codigo, CancellationToken ct = default)
    {
        if (!perfil.EmailConfirmado)
            return Resultado<string>.Falha(ChavesDeErro.EmailNaoConfirmado);
        Mesa? mesa;
        lock (_registro)
            _porCodigo.TryGetValue(codigo.Trim().ToUpperInvariant(), out mesa);
        if (mesa is null)
            return Resultado<string>.Falha(ChavesDeErro.SalaNaoEncontrada);

        await mesa.Trava.WaitAsync(ct);
        try
        {
            lock (_registro)
            {
                if (_porConta.TryGetValue(perfil.ContaId, out var atual))
                    return atual == mesa
                        ? Resultado<string>.Ok(mesa.Codigo)
                        : Resultado<string>.Falha(ChavesDeErro.JaEstaEmSala);
            }
            if (mesa.Fase != FaseDaSala.Espera)
                return Resultado<string>.Falha(ChavesDeErro.PartidaEmAndamento);
            if (mesa.Participantes.Count >= mesa.Maximo)
                return Resultado<string>.Falha(ChavesDeErro.SalaCheia);

            mesa.Participantes.Add(Humano(perfil));
            lock (_registro)
                _porConta[perfil.ContaId] = mesa;
            var agora = relogio.AgoraUtc;
            await trabalho.ExecutarAsync(repositorio =>
            {
                repositorio.Adicionar(new Ent.SalaJogador { SalaId = mesa.SalaId, JogadorId = perfil.ContaId, EntrouEm = agora });
                return repositorio.SalvarAsync(ct);
            }, ct);
            await TransmitirAsync(mesa);
            return Resultado<string>.Ok(mesa.Codigo);
        }
        finally
        {
            mesa.Trava.Release();
        }
    }

    /// <summary>Na espera, levanta da sala; durante a partida, desiste (sai ao fim da mão).</summary>
    public async Task<Resultado> SairAsync(Guid contaId, CancellationToken ct = default)
    {
        var mesa = MesaDe(contaId);
        if (mesa is null)
            return Resultado.Falha(ChavesDeErro.NaoEstaEmSala);

        await mesa.Trava.WaitAsync(ct);
        try
        {
            var participante = mesa.Participantes.First(p => p.ContaId.GetValueOrDefault() == contaId);
            if (mesa.Fase == FaseDaSala.EmPartida && participante.Jogador!.EstaNaPartida)
            {
                var desistencia = mesa.Partida!.Desistir(participante.Jogador);
                if (!desistencia.Sucesso)
                    return desistencia;
                Registrar(mesa, "Partida_DiarioDesistiu", participante.Apelido);
            }

            Desconectar(participante);
            var agora = relogio.AgoraUtc;
            await trabalho.ExecutarAsync(async repositorio =>
            {
                var vinculo = await repositorio.BuscarAsync<Ent.SalaJogador>(s => s.SalaId == mesa.SalaId && s.JogadorId == contaId && s.SaiuEm == null, ct);
                if (vinculo is not null)
                    vinculo.SaiuEm = agora;
                await repositorio.SalvarAsync(ct);
            }, ct);

            if (mesa.Fase == FaseDaSala.Espera)
                mesa.Participantes.Remove(participante);
            await ReorganizarAposSaidaAsync(mesa, ct);
            return Resultado.Ok;
        }
        finally
        {
            mesa.Trava.Release();
        }
    }

    public async Task<Resultado> PreencherComBotsAsync(Guid contaId, int quantidade, CancellationToken ct = default)
    {
        var mesa = MesaDe(contaId);
        if (mesa is null)
            return Resultado.Falha(ChavesDeErro.NaoEstaEmSala);

        await mesa.Trava.WaitAsync(ct);
        try
        {
            if (mesa.CriadorId != contaId)
                return Resultado.Falha(ChavesDeErro.SomenteOCriador);
            if (mesa.Fase != FaseDaSala.Espera)
                return Resultado.Falha(ChavesDeErro.PartidaEmAndamento);
            if (quantidade < 1)
                return Resultado.Falha(ChavesDeErro.SalaInvalida);

            var nomesLivres = NomesDeBots.Where(n => mesa.Participantes.All(p => p.Apelido != n)).ToList();
            var vagas = Math.Min(quantidade, mesa.Maximo - mesa.Participantes.Count);
            for (var i = 0; i < vagas; i++)
            {
                var nome = nomesLivres[sorteador.Sortear(nomesLivres.Count)];
                nomesLivres.Remove(nome);
                mesa.Participantes.Add(new Participante
                {
                    Apelido = nome,
                    EhBot = true,
                    Cor = CoresDeBots[Array.IndexOf(NomesDeBots, nome) % CoresDeBots.Length],
                });
            }
            await TransmitirAsync(mesa);
            return Resultado.Ok;
        }
        finally
        {
            mesa.Trava.Release();
        }
    }

    public async Task<Resultado> IniciarPartidaAsync(Guid contaId, CancellationToken ct = default)
    {
        var mesa = MesaDe(contaId);
        if (mesa is null)
            return Resultado.Falha(ChavesDeErro.NaoEstaEmSala);

        await mesa.Trava.WaitAsync(ct);
        try
        {
            if (mesa.CriadorId != contaId)
                return Resultado.Falha(ChavesDeErro.SomenteOCriador);
            if (mesa.Fase != FaseDaSala.Espera)
                return Resultado.Falha(ChavesDeErro.PartidaEmAndamento);
            if (mesa.Participantes.Count < ConfiguracaoDoJogo.MinimoDeJogadores)
                return Resultado.Falha(ChavesDeErro.JogadoresInsuficientes);

            foreach (var participante in mesa.Participantes)
                participante.Jogador = new Jogador(participante.Apelido, participante.EhBot);
            mesa.Partida = new Partida(mesa.Participantes.Select(p => p.Jogador!), sorteador);
            mesa.Assentos.AddRange(mesa.Partida.Assentos.Select(j => mesa.Participantes.First(p => p.Jogador == j)));
            mesa.Fase = FaseDaSala.EmPartida;
            mesa.PartidaId = Guid.NewGuid();
            await PersistirInicioAsync(mesa, ct);

            Registrar(mesa, "Partida_DiarioInicio", Nome(mesa, mesa.Partida.Distribuidor));
            await IniciarMaoAsync(mesa, ct);
            return Resultado.Ok;
        }
        finally
        {
            mesa.Trava.Release();
        }
    }

    // ---------- ações da partida ----------

    public Task<Resultado> DecidirAsync(Guid contaId, bool ficar, CancellationToken ct = default) =>
        ComandoAsync(contaId, (mesa, jogador) =>
        {
            var resultado = mesa.Mao!.Decidir(ficar);
            if (resultado.Sucesso)
                Registrar(mesa, ficar ? "Partida_DiarioFica" : "Partida_DiarioCorre", jogador.Apelido);
            return resultado;
        }, ct);

    public Task<Resultado> PegarBauAsync(Guid contaId, int numero, CancellationToken ct = default) =>
        ComandoAsync(contaId, (mesa, jogador) =>
        {
            var resultado = mesa.Mao!.PegarBau(numero);
            if (!resultado.Sucesso)
                return resultado;
            var taxa = mesa.Mao.TaxaDoBau(numero);
            Registrar(mesa, "Partida_DiarioBau", jogador.Apelido, (numero + 1).ToString());
            if (taxa > 0)
                Registrar(mesa, "Partida_DiarioTaxa", jogador.Apelido, taxa.ToString(), mesa.Mao.Montante.ToString());
            return resultado;
        }, ct);

    public Task<Resultado> JogarCartaAsync(Guid contaId, int naipe, int valor, CancellationToken ct = default) =>
        ComandoAsync(contaId, (mesa, _) => mesa.Mao!.JogarCarta(new Carta((Naipe)naipe, (Valor)valor)), ct);

    public async Task<Resultado> EnviarEmoteAsync(Guid contaId, int emote, CancellationToken ct = default)
    {
        var mesa = MesaDe(contaId);
        if (mesa is null)
            return Resultado.Falha(ChavesDeErro.NaoEstaEmSala);
        if (emote is < 0 or >= EmotesDisponiveis)
            return Resultado.Falha(ChavesDeErro.AcaoForaDeHora);

        await mesa.Trava.WaitAsync(ct);
        try
        {
            var agora = relogio.AgoraUtc;
            if (mesa.UltimoEmote.TryGetValue(contaId, out var ultimo) && agora - ultimo < IntervaloDeEmote)
                return Resultado.Falha(ChavesDeErro.EmoteMuitoRapido);
            mesa.UltimoEmote[contaId] = agora;

            var origem = mesa.Fase == FaseDaSala.Espera ? mesa.Participantes : mesa.Assentos;
            var assento = origem.FindIndex(p => p.ContaId == contaId);
            await EmitirEmoteAsync(mesa, assento, emote);
            return Resultado.Ok;
        }
        finally
        {
            mesa.Trava.Release();
        }
    }

    // ---------- motor ----------

    private async Task<Resultado> ComandoAsync(Guid contaId, Func<Mesa, Participante, Resultado> acao, CancellationToken ct)
    {
        var mesa = MesaDe(contaId);
        if (mesa is null)
            return Resultado.Falha(ChavesDeErro.NaoEstaEmSala);

        await mesa.Trava.WaitAsync(ct);
        try
        {
            if (mesa.Fase != FaseDaSala.EmPartida || mesa.Mao is null || mesa.EmPausa)
                return Resultado.Falha(ChavesDeErro.AcaoForaDeHora);
            var participante = mesa.Participantes.First(p => p.ContaId.GetValueOrDefault() == contaId);
            if (mesa.Mao.JogadorDaVez != participante.Jogador)
                return Resultado.Falha(ChavesDeErro.NaoEhSuaVez);

            var resultado = acao(mesa, participante);
            if (resultado.Sucesso)
                await AvancarAsync(mesa, ct);
            return resultado;
        }
        finally
        {
            mesa.Trava.Release();
        }
    }

    private async Task IniciarMaoAsync(Mesa mesa, CancellationToken ct)
    {
        mesa.Mao = mesa.Partida!.IniciarMao();
        mesa.NumeroDaMao++;
        mesa.RodadasVistas = 0;
        mesa.RodadaEmExibicao = null;
        Registrar(mesa, "Partida_DiarioMao", mesa.NumeroDaMao.ToString(), Nome(mesa, mesa.Mao.Distribuidor), mesa.Mao.Montante.ToString());
        await AvancarAsync(mesa, ct);
    }

    /// <summary>Com a trava em mãos: resolve o que o estado pede (pausa, fim de mão, vez de bot ou de humano).</summary>
    private async Task AvancarAsync(Mesa mesa, CancellationToken ct)
    {
        var mao = mesa.Mao!;
        var rodadaNova = mao.Rodadas.Count > mesa.RodadasVistas;
        if (rodadaNova)
        {
            mesa.RodadasVistas = mao.Rodadas.Count;
            mesa.RodadaEmExibicao = mao.Rodadas[^1];
            var rodada = mesa.RodadaEmExibicao;
            Registrar(mesa, "Partida_DiarioRodada", rodada.Numero.ToString(), Nome(mesa, rodada.Vencedor),
                CartaTexto(rodada.Jogadas.First(j => j.Jogador == rodada.Vencedor).Carta), rodada.Valor.ToString());
            ReagirComEmotes(mesa, rodada.Jogadas.Select(j => j.Jogador), rodada.Vencedor);
        }

        if (mao.Fase == FaseDaMao.Concluida)
        {
            if (mao.Desfecho == DesfechoDaMao.UnicoQueFicou)
            {
                var unico = mao.Movimentos.Last().Jogador;
                ReagirComEmotes(mesa, [unico], unico);
            }
            RegistrarFimDaMao(mesa, mao);
            await PersistirMaoAsync(mesa, mao, ct);
            mesa.PrazoFim = null;
            Pausar(mesa, PausaDaMao, ProximaMaoAsync);
            await TransmitirAsync(mesa);
            return;
        }

        if (rodadaNova)
        {
            mesa.PrazoFim = null;
            Pausar(mesa, PausaDaRodada, ContinuarAposRodadaAsync);
            await TransmitirAsync(mesa);
            return;
        }

        var vez = mao.JogadorDaVez!;
        var participante = mesa.Participantes.First(p => p.Jogador == vez);
        if (participante.EhBot)
        {
            mesa.PrazoFim = null;
            Agendar(mesa, TimeSpan.FromMilliseconds(700 + sorteador.Sortear(801)), AgirBotAsync);
        }
        else
        {
            mesa.PrazoSegundos = mao.Fase == FaseDaMao.Decisao ? ConfiguracaoDoJogo.SegundosParaDecidir : ConfiguracaoDoJogo.SegundosParaJogarCarta;
            mesa.PrazoFim = relogio.AgoraUtc.AddSeconds(mesa.PrazoSegundos);
            Agendar(mesa, TimeSpan.FromSeconds(mesa.PrazoSegundos), TempoEsgotadoAsync);
        }
        await TransmitirAsync(mesa);
    }

    /// <summary>Como no protótipo: depois de uma rodada, cada bot tem 40% de chance de reagir com um emote.</summary>
    private void ReagirComEmotes(Mesa mesa, IEnumerable<Jogador> jogaram, Jogador vencedor)
    {
        foreach (var jogador in jogaram)
        {
            var participante = mesa.Participantes.First(p => p.Jogador == jogador);
            if (!participante.EhBot || sorteador.Sortear(100) >= 40)
                continue;

            var opcoes = jogador == vencedor ? EmotesDeVitoria : EmotesDeDerrota;
            var emote = opcoes[sorteador.Sortear(opcoes.Length)];
            var assento = mesa.Assentos.IndexOf(participante);
            var espera = relogio.EsperarAsync(TimeSpan.FromMilliseconds(300 + sorteador.Sortear(1201)));
            _ = Task.Run(async () =>
            {
                await espera;
                await EmitirEmoteAsync(mesa, assento, emote);
            });
        }
    }

    private Task EmitirEmoteAsync(Mesa mesa, int assento, int emote) =>
        Task.WhenAll(mesa.Participantes.Where(p => p.Conectado)
            .Select(p => Seguro(mesa, () => notificador.EmoteAsync(p.ContaId!.Value, assento, emote))));

    private void Pausar(Mesa mesa, TimeSpan duracao, Func<Mesa, int, Task> acao)
    {
        mesa.EmPausa = true;
        Agendar(mesa, duracao, acao);
    }

    private void Agendar(Mesa mesa, TimeSpan duracao, Func<Mesa, int, Task> acao)
    {
        var geracao = ++mesa.Geracao;
        var espera = relogio.EsperarAsync(duracao);
        mesa.Tarefa = Task.Run(async () =>
        {
            try
            {
                await espera;
                await mesa.Trava.WaitAsync();
                try
                {
                    if (mesa.Geracao == geracao)
                        await acao(mesa, geracao);
                }
                finally
                {
                    mesa.Trava.Release();
                }
            }
            catch (Exception erro)
            {
                mesa.UltimoErro = erro;
            }
        });
    }

    private async Task AgirBotAsync(Mesa mesa, int geracao)
    {
        _bots.AgirNaVez(mesa.Mao!);
        await AvancarAsync(mesa, CancellationToken.None);
    }

    private async Task TempoEsgotadoAsync(Mesa mesa, int geracao)
    {
        var mao = mesa.Mao!;
        var nome = Nome(mesa, mao.JogadorDaVez!);
        if (mao.Fase == FaseDaMao.Decisao)
        {
            mao.TempoEsgotadoNaDecisao();
            Registrar(mesa, "Partida_DiarioTempoDecisao", nome);
        }
        else
        {
            mao.TempoEsgotadoNaJogada();
            Registrar(mesa, "Partida_DiarioTempoJogada", nome);
        }
        await AvancarAsync(mesa, CancellationToken.None);
    }

    private async Task ContinuarAposRodadaAsync(Mesa mesa, int geracao)
    {
        mesa.EmPausa = false;
        mesa.RodadaEmExibicao = null;
        await AvancarAsync(mesa, CancellationToken.None);
    }

    private async Task ProximaMaoAsync(Mesa mesa, int geracao)
    {
        mesa.EmPausa = false;
        mesa.RodadaEmExibicao = null;
        var partida = mesa.Partida!;
        var antes = partida.Vivos.ToList();
        partida.EncerrarMao();
        foreach (var saiu in antes.Where(j => !j.EstaNaPartida))
        {
            var chave = saiu.MotivoDaSaida switch
            {
                MotivoDaSaida.SemFichas => "Partida_DiarioSemFichas",
                MotivoDaSaida.Inatividade => "Partida_DiarioInatividade",
                _ => "Partida_DiarioSaiu",
            };
            Registrar(mesa, chave, Nome(mesa, saiu));
        }

        if (partida.Encerrada)
        {
            await FinalizarPartidaAsync(mesa, CancellationToken.None);
            return;
        }
        await IniciarMaoAsync(mesa, CancellationToken.None);
    }

    private async Task FinalizarPartidaAsync(Mesa mesa, CancellationToken ct)
    {
        mesa.Fase = FaseDaSala.Encerrada;
        mesa.Mao = null;
        mesa.PrazoFim = null;
        mesa.Geracao++;
        var ranking = mesa.Partida!.Ranking.ToList();
        Registrar(mesa, "Partida_DiarioFim", Nome(mesa, ranking[0]));
        await PersistirFimAsync(mesa, ranking, interrompida: false, ct);
        await TransmitirAsync(mesa);
    }

    private async Task ReorganizarAposSaidaAsync(Mesa mesa, CancellationToken ct)
    {
        var humanosAtivos = mesa.Participantes.Where(p => !p.EhBot && p.Conectado).ToList();
        if (humanosAtivos.Count == 0)
        {
            mesa.Geracao++;
            if (mesa.Fase == FaseDaSala.EmPartida)
            {
                mesa.Fase = FaseDaSala.Encerrada;
                await PersistirFimAsync(mesa, mesa.Partida!.Ranking.ToList(), interrompida: true, ct);
            }
            else
            {
                await trabalho.ExecutarAsync(async repositorio =>
                {
                    var sala = await repositorio.BuscarAsync<Ent.Sala>(s => s.Id == mesa.SalaId, ct);
                    sala!.Situacao = Ent.SituacaoDaSala.Encerrada;
                    await repositorio.SalvarAsync(ct);
                }, ct);
            }
            lock (_registro)
                _porCodigo.Remove(mesa.Codigo);
            return;
        }

        if (humanosAtivos.All(p => p.ContaId.GetValueOrDefault() != mesa.CriadorId))
            mesa.CriadorId = humanosAtivos[0].ContaId!.Value;
        await TransmitirAsync(mesa);
    }

    // ---------- persistência ----------

    private Task PersistirInicioAsync(Mesa mesa, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        return trabalho.ExecutarAsync(async repositorio =>
        {
            repositorio.Adicionar(new Ent.Partida
            {
                Id = mesa.PartidaId,
                SalaId = mesa.SalaId,
                IniciadaEm = agora,
                QuantidadeDeJogadores = mesa.Assentos.Count,
                Situacao = Ent.SituacaoDaPartida.EmAndamento,
            });
            for (var assento = 0; assento < mesa.Assentos.Count; assento++)
            {
                var p = mesa.Assentos[assento];
                repositorio.Adicionar(new Ent.PartidaJogador
                {
                    PartidaId = mesa.PartidaId,
                    Assento = assento,
                    JogadorId = p.ContaOriginal,
                    NomeDoBot = p.EhBot ? p.Apelido : null,
                    FichasIniciais = ConfiguracaoDoJogo.FichasIniciais,
                    FichasFinais = ConfiguracaoDoJogo.FichasIniciais,
                });
            }
            var sala = await repositorio.BuscarAsync<Ent.Sala>(s => s.Id == mesa.SalaId, ct);
            sala!.Situacao = Ent.SituacaoDaSala.EmPartida;
            await repositorio.SalvarAsync(ct);
        }, ct);
    }

    private Task PersistirMaoAsync(Mesa mesa, Mao mao, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        int Assento(Jogador j) => mesa.Assentos.FindIndex(p => p.Jogador == j);
        return trabalho.ExecutarAsync(async repositorio =>
        {
            var maoId = Guid.NewGuid();
            repositorio.Adicionar(new Ent.Mao
            {
                Id = maoId,
                PartidaId = mesa.PartidaId,
                Numero = mesa.NumeroDaMao,
                AssentoDoDistribuidor = Assento(mao.Distribuidor),
                Mania = mao.Mania,
                Montante = mao.Montante,
                TaxaDeBau = mao.TaxaDoBau(0) + mao.TaxaDoBau(1),
                Desfecho = mao.Desfecho!.Value,
            });
            foreach (var jogador in mao.Participantes)
            {
                repositorio.Adicionar(new Ent.MaoJogador
                {
                    MaoId = maoId,
                    Assento = Assento(jogador),
                    Decisao = mao.DecisaoDe(jogador),
                    BausPegos = mao.BausPegosPor(jogador),
                    RodadasVencidas = mao.Vitorias(jogador),
                    ReposicaoPaga = -mao.Movimentos.Where(m => m.Jogador == jogador && m.Tipo == TipoDeMovimento.ReposicaoDoMontante).Sum(m => m.Valor),
                });
            }
            for (var numero = 0; numero < 2; numero++)
            {
                var dono = mao.DonoDoBau(numero);
                repositorio.Adicionar(new Ent.Bau
                {
                    Id = Guid.NewGuid(),
                    MaoId = maoId,
                    Numero = numero + 1,
                    PegoPorAssento = dono is null ? null : Assento(dono),
                    TaxaPaga = mao.TaxaDoBau(numero),
                });
            }
            foreach (var rodada in mao.Rodadas)
            {
                var rodadaId = Guid.NewGuid();
                repositorio.Adicionar(new Ent.Rodada
                {
                    Id = rodadaId,
                    MaoId = maoId,
                    Numero = rodada.Numero,
                    AbridorAssento = Assento(rodada.Abridor),
                    VencedorAssento = Assento(rodada.Vencedor),
                    Valor = rodada.Valor,
                });
                for (var ordem = 0; ordem < rodada.Jogadas.Count; ordem++)
                {
                    var jogada = rodada.Jogadas[ordem];
                    repositorio.Adicionar(new Ent.Jogada
                    {
                        Id = Guid.NewGuid(),
                        RodadaId = rodadaId,
                        Assento = Assento(jogada.Jogador),
                        Ordem = ordem + 1,
                        Naipe = jogada.Carta.Naipe,
                        Valor = jogada.Carta.Valor,
                        FoiAutomatica = jogada.FoiAutomatica,
                        FoiVirada = jogada.FoiAutomatica,
                    });
                }
            }
            foreach (var movimento in mao.Movimentos)
            {
                repositorio.Adicionar(new Ent.MovimentoDeFicha
                {
                    Id = Guid.NewGuid(),
                    PartidaId = mesa.PartidaId,
                    MaoId = maoId,
                    Assento = Assento(movimento.Jogador),
                    Tipo = movimento.Tipo,
                    Valor = movimento.Valor,
                    OcorreuEm = agora,
                });
            }

            var linhas = await repositorio.ListarAsync<Ent.PartidaJogador>(l => l.PartidaId == mesa.PartidaId, ct);
            foreach (var linha in linhas)
                linha.FichasFinais = mesa.Assentos[linha.Assento].Jogador!.Fichas;
            await repositorio.SalvarAsync(ct);
        }, ct);
    }

    private Task PersistirFimAsync(Mesa mesa, List<Jogador> ranking, bool interrompida, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        return trabalho.ExecutarAsync(async repositorio =>
        {
            var partida = await repositorio.BuscarAsync<Ent.Partida>(p => p.Id == mesa.PartidaId, ct);
            partida!.EncerradaEm = agora;
            partida.Situacao = interrompida ? Ent.SituacaoDaPartida.Interrompida : Ent.SituacaoDaPartida.Encerrada;
            var vencedor = mesa.Participantes.First(p => p.Jogador == ranking[0]);
            partida.VencedorJogadorId = interrompida ? null : vencedor.ContaOriginal;

            var linhas = await repositorio.ListarAsync<Ent.PartidaJogador>(l => l.PartidaId == mesa.PartidaId, ct);
            foreach (var linha in linhas)
            {
                var jogador = mesa.Assentos[linha.Assento].Jogador!;
                linha.FichasFinais = jogador.Fichas;
                linha.Colocacao = ranking.IndexOf(jogador) + 1;
                linha.MotivoDaSaida = jogador.MotivoDaSaida;
            }
            var sala = await repositorio.BuscarAsync<Ent.Sala>(s => s.Id == mesa.SalaId, ct);
            sala!.Situacao = Ent.SituacaoDaSala.Encerrada;
            await repositorio.SalvarAsync(ct);
        }, ct);
    }

    // ---------- visão e eventos ----------

    private async Task TransmitirAsync(Mesa mesa)
    {
        var destinos = mesa.Participantes.Where(p => p.Conectado).Select(p => p.ContaId!.Value).ToList();
        await Task.WhenAll(destinos.Select(conta => Seguro(mesa, () => notificador.MesaAtualizadaAsync(conta, Visao(mesa, conta)))));
    }

    private static async Task Seguro(Mesa mesa, Func<Task> envio)
    {
        try
        {
            await envio();
        }
        catch (Exception erro)
        {
            mesa.UltimoErro = erro;
        }
    }

    private VisaoDaMesa Visao(Mesa mesa, Guid contaId)
    {
        var eu = mesa.Participantes.First(p => p.ContaId.GetValueOrDefault() == contaId);
        var sentados = mesa.Partida is null ? mesa.Participantes : mesa.Assentos;
        var mao = mesa.Mao;
        var meuAssento = sentados.IndexOf(eu);
        int? Assento(Jogador? j) => j is null ? null : sentados.FindIndex(p => p.Jogador == j);

        var assentos = sentados.Select((p, i) => new AssentoVisao(
            i, p.Apelido, p.Cor, p.EhBot, p == eu,
            p.Jogador?.Fichas ?? ConfiguracaoDoJogo.FichasIniciais,
            p.Jogador?.EstaNaPartida ?? true,
            p.Jogador?.MotivoDaSaida?.ToString(),
            mao is not null && mao.Distribuidor == p.Jogador,
            NaMao(mao, p) && mao!.JaDecidiu(p.Jogador!) ? mao.DecisaoDe(p.Jogador!).ToString() : null,
            NaMao(mao, p) ? mao!.BausPegosPor(p.Jogador!) : 0,
            NaMao(mao, p) ? mao!.Vitorias(p.Jogador!) : 0)).ToList();

        var minhaMao = new List<CartaVisao>();
        var permitidas = new List<int>();
        DecisaoVisao? decisao = null;
        var baus = new List<BauVisao>();
        var suaVez = mao is not null && !mesa.EmPausa && mao.JogadorDaVez == eu.Jogador;
        if (NaMao(mao, eu))
        {
            var cartas = mao!.CartasDe(eu.Jogador!);
            minhaMao.AddRange(cartas.Select(c => new CartaVisao((int)c.Naipe, (int)c.Valor)));
            if (suaVez && mao.Fase == FaseDaMao.Rodadas)
            {
                var legais = mao.CartasPermitidas();
                permitidas.AddRange(Enumerable.Range(0, cartas.Count).Where(i => legais.Contains(cartas[i])));
            }
            if (suaVez && mao.Fase == FaseDaMao.Decisao)
                decisao = new DecisaoVisao(mao.PodeFicar(eu.Jogador!), mao.BausPegosPor(eu.Jogador!) == 0, mao.BausPegosPor(eu.Jogador!) > 0);
        }
        if (mao is { Fase: FaseDaMao.Decisao })
        {
            baus.AddRange(Enumerable.Range(0, 2).Select(n =>
                new BauVisao(n, Assento(mao.DonoDoBau(n)), suaVez && mao.Fase == FaseDaMao.Decisao && mao.PodePegarBau(n))));
        }

        var exibindo = mesa.RodadaEmExibicao;
        var jogadas = exibindo is not null
            ? exibindo.Jogadas.Select(j => new JogadaVisao(Assento(j.Jogador)!.Value, new CartaVisao((int)j.Carta.Naipe, (int)j.Carta.Valor), false)).ToList()
            : (mao is null ? [] : mao.JogadasDaRodada).Select(j => new JogadaVisao(
                Assento(j.Jogador)!.Value, j.FoiAutomatica ? null : new CartaVisao((int)j.Carta.Naipe, (int)j.Carta.Valor), j.FoiAutomatica)).ToList();

        string? etapa = null;
        if (mao is not null)
        {
            etapa = exibindo is not null || mao.Fase == FaseDaMao.Concluida ? "fim"
                : mao.Fase == FaseDaMao.Decisao ? "decisao" : "rodadas";
        }

        var fase = mesa.Fase switch { FaseDaSala.Espera => "espera", FaseDaSala.EmPartida => "partida", _ => "encerrada" };
        var emPausa = mesa.EmPausa || mao is null;
        return new VisaoDaMesa(
            mesa.Codigo, mesa.Nome, fase, mesa.CriadorId == contaId, mesa.Maximo,
            mesa.Participantes.Count(p => p.EhBot),
            assentos, meuAssento,
            mesa.NumeroDaMao, mao?.Montante ?? 0, mao?.ValorDaRodada ?? 0,
            mao is null ? null : new CartaVisao((int)mao.CartaDaMania.Naipe, (int)mao.CartaDaMania.Valor),
            etapa,
            emPausa ? null : Assento(mao!.JogadorDaVez), emPausa ? null : Assento(mao!.ProximoJogador),
            emPausa ? null : Assento(mao!.Abridor), mao?.Rodada ?? 0,
            minhaMao, permitidas, baus, decisao, jogadas,
            exibindo is null ? null : Assento(exibindo.Vencedor),
            exibindo is null ? [] : exibindo.Jogadas.Where(j => j.Jogador != exibindo.Vencedor).Select(j => Assento(j.Jogador)!.Value).ToList(),
            mesa.PrazoSegundos, mesa.PrazoFim, relogio.AgoraUtc,
            mesa.Diario.ToList(), mesa.Aviso);
    }

    private void RegistrarFimDaMao(Mesa mesa, Mao mao)
    {
        switch (mao.Desfecho)
        {
            case DesfechoDaMao.TodosCorreram:
                Registrar(mesa, "Partida_DiarioTodosCorreram", Nome(mesa, mao.Distribuidor), mao.Montante.ToString());
                break;
            case DesfechoDaMao.UnicoQueFicou:
                var unico = mao.Movimentos.Last().Jogador;
                Registrar(mesa, "Partida_DiarioUnico", Nome(mesa, unico), mao.Montante.ToString());
                break;
            default:
                foreach (var reposicao in mao.Movimentos.Where(m => m.Tipo == TipoDeMovimento.ReposicaoDoMontante))
                    Registrar(mesa, "Partida_DiarioReposicao", Nome(mesa, reposicao.Jogador), (-reposicao.Valor).ToString());
                break;
        }
    }

    private static void Registrar(Mesa mesa, string chave, params string[] argumentos)
    {
        var evento = new EventoVisao(chave, argumentos);
        mesa.Diario.Add(evento);
        if (mesa.Diario.Count > MaximoDoDiario)
            mesa.Diario.RemoveAt(0);
        mesa.Aviso = evento;
    }

    /// <summary>Quem já saiu da partida não faz parte das mãos seguintes.</summary>
    private static bool NaMao(Mao? mao, Participante participante) =>
        mao is not null && mao.Participantes.Contains(participante.Jogador!);

    private static string Nome(Mesa mesa, Jogador jogador) => mesa.Participantes.First(p => p.Jogador == jogador).Apelido;

    private static string CartaTexto(Carta carta) => Rotulos[(int)carta.Valor] + Simbolos[(int)carta.Naipe];

    // ---------- utilidades ----------

    private Mesa? MesaDe(Guid contaId)
    {
        lock (_registro)
            return _porConta.TryGetValue(contaId, out var mesa) ? mesa : null;
    }

    private void Desconectar(Participante participante)
    {
        var conta = participante.ContaId!.Value;
        lock (_registro)
            _porConta.Remove(conta);
        participante.ContaId = null;
    }

    private static Participante Humano(PerfilDoJogador perfil) =>
        new() { ContaOriginal = perfil.ContaId, ContaId = perfil.ContaId, Apelido = perfil.Apelido, Cor = perfil.Cor };

    private string NovoCodigo()
    {
        string codigo;
        do
        {
            codigo = new string(Enumerable.Range(0, TamanhoDoCodigo).Select(_ => Alfabeto[sorteador.Sortear(Alfabeto.Length)]).ToArray());
        }
        while (_porCodigo.ContainsKey(codigo));
        return codigo;
    }
}
