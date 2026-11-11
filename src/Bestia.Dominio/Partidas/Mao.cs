using Bestia.Dominio.Cartas;
using Bestia.Dominio.Jogadores;

namespace Bestia.Dominio.Partidas;

public enum FaseDaMao { Decisao, Rodadas, Concluida }

public enum DesfechoDaMao { TodosCorreram, UnicoQueFicou, Disputada }

public sealed record Jogada(Jogador Jogador, Carta Carta, bool FoiAutomatica);

public enum DecisaoNaMao : byte { Ficou, Correu, Ausente }

public enum TipoDeMovimento : byte
{
    ApostaDoDistribuidor,
    TaxaDoBau,
    PremioDaRodada,
    ReposicaoDoMontante,
    MontanteDoDistribuidor,
    PremioDoUnicoQueFicou,
}

/// <summary>Uma linha do livro-caixa da mão: positivo entra nas fichas do jogador, negativo sai.</summary>
public sealed record Movimento(Jogador Jogador, TipoDeMovimento Tipo, int Valor);

public sealed record RodadaDisputada(int Numero, Jogador Abridor, Jogador Vencedor, int Valor, IReadOnlyList<Jogada> Jogadas);

/// <summary>
/// Uma mão: aposta, decisões, baús, três rodadas e reposição. Os prazos (20 s e 45 s) ficam com
/// quem chama: ao estourar, chame TempoEsgotadoNaDecisao ou TempoEsgotadoNaJogada.
/// </summary>
public sealed class Mao
{
    private sealed class Estado(List<Carta> cartas)
    {
        public List<Carta> Cartas { get; set; } = cartas;
        public bool Ficou { get; set; }
        public int BausPegos { get; set; }
        public int Vitorias { get; set; }
        public int Ausencias { get; set; }
        public DecisaoNaMao Decisao { get; set; } = DecisaoNaMao.Correu;
        public bool Decidiu { get; set; }
    }

    private readonly Dictionary<Jogador, Estado> _estados = [];
    private readonly List<Jogador> _ordem;
    private readonly List<Carta>[] _baus;
    private readonly Jogador?[] _donosDosBaus = new Jogador?[2];
    private readonly int[] _taxasDosBaus = new int[2];
    private readonly List<Movimento> _movimentos = [];
    private readonly List<RodadaDisputada> _rodadas = [];
    private readonly List<Jogada> _jogadas = [];
    private List<Jogador> _sequencia = [];
    private int _indice;
    private int _rodada;

    /// <param name="baralho">Ordem de saque: mãos dos vivos (em ordem de assento), mania, baú 1, baú 2.</param>
    public Mao(IReadOnlyList<Jogador> vivos, Jogador distribuidor, int montantePendente, IReadOnlyList<Carta> baralho)
    {
        Distribuidor = distribuidor;
        distribuidor.Fichas -= ConfiguracaoDoJogo.ApostaDoDistribuidor;
        Montante = montantePendente + ConfiguracaoDoJogo.ApostaDoDistribuidor;
        _movimentos.Add(new Movimento(distribuidor, TipoDeMovimento.ApostaDoDistribuidor, -ConfiguracaoDoJogo.ApostaDoDistribuidor));

        var proxima = 0;
        List<Carta> Sacar()
        {
            var cartas = baralho.Skip(proxima).Take(ConfiguracaoDoJogo.CartasPorMao).ToList();
            proxima += ConfiguracaoDoJogo.CartasPorMao;
            return cartas;
        }

        foreach (var jogador in vivos)
            _estados[jogador] = new Estado(Sacar());
        CartaDaMania = baralho[proxima++];
        _baus = [Sacar(), Sacar()];

        var lista = vivos.ToList();
        var depoisDoDistribuidor = lista.IndexOf(distribuidor) + 1;
        _ordem = lista.Skip(depoisDoDistribuidor).Concat(lista.Take(depoisDoDistribuidor)).ToList();
        Participantes = lista;
    }

    public IReadOnlyList<Jogador> Participantes { get; }

    public IReadOnlyList<Movimento> Movimentos => _movimentos;

    public IReadOnlyList<RodadaDisputada> Rodadas => _rodadas;

    /// <summary>Número da rodada em andamento (1 a 3); 0 antes das rodadas.</summary>
    public int Rodada => _rodada;

    public Jogador? Abridor => Fase == FaseDaMao.Rodadas ? _sequencia[0] : null;

    public Jogador? ProximoJogador => Fase switch
    {
        FaseDaMao.Decisao => _indice + 1 < _ordem.Count ? _ordem[_indice + 1] : null,
        FaseDaMao.Rodadas => _jogadas.Count + 1 < _sequencia.Count ? _sequencia[_jogadas.Count + 1] : null,
        _ => null,
    };

    public DecisaoNaMao DecisaoDe(Jogador jogador) => _estados[jogador].Decisao;

    public bool JaDecidiu(Jogador jogador) => _estados[jogador].Decidiu;

    public int BausPegosPor(Jogador jogador) => _estados[jogador].BausPegos;

    public int Vitorias(Jogador jogador) => _estados[jogador].Vitorias;

    public Jogador? DonoDoBau(int numero) => _donosDosBaus[numero];

    public int TaxaDoBau(int numero) => _taxasDosBaus[numero];

    public FaseDaMao Fase { get; private set; } = FaseDaMao.Decisao;

    public DesfechoDaMao? Desfecho { get; private set; }

    public Jogador Distribuidor { get; }

    public int Montante { get; private set; }

    /// <summary>Fichas repostas por quem perdeu as três rodadas; vale após a mão concluir.</summary>
    public int MontantePendente { get; private set; }

    public int ValorDaRodada => Montante / ConfiguracaoDoJogo.RodadasPorMao;

    public Carta CartaDaMania { get; }

    public Naipe Mania => CartaDaMania.Naipe;

    public IReadOnlyList<Jogada> JogadasDaRodada => _jogadas;

    public Jogador? JogadorDaVez => Fase switch
    {
        FaseDaMao.Decisao => _ordem[_indice],
        FaseDaMao.Rodadas => _sequencia[_jogadas.Count],
        _ => null,
    };

    public bool PodeFicar(Jogador jogador) => jogador.Fichas >= Montante;

    /// <summary>Se o jogador da vez poderia pegar esse baú agora (mesmas regras de PegarBau, sem efeito).</summary>
    public bool PodePegarBau(int numero) =>
        Fase == FaseDaMao.Decisao
        && numero is >= 0 and <= 1
        && _donosDosBaus[numero] is null
        && PodeFicar(_ordem[_indice])
        && (_estados[_ordem[_indice]].BausPegos == 0 || PodePagarTaxa(_ordem[_indice]));

    public IReadOnlyList<Carta> CartasDe(Jogador jogador) => _estados[jogador].Cartas;

    public int Ausencias(Jogador jogador) => _estados[jogador].Ausencias;

    public IEnumerable<int> BausLivres() => Enumerable.Range(0, _baus.Length).Where(n => _donosDosBaus[n] is null);

    public IReadOnlyList<Carta> CartasPermitidas()
    {
        var cartas = _estados[JogadorDaVez!].Cartas;
        if (_jogadas.Count == 0)
            return cartas;
        var naipeAberto = _jogadas[0].Carta.Naipe;
        return cartas.Any(c => c.Naipe == naipeAberto) ? cartas.Where(c => c.Naipe == naipeAberto).ToList() : cartas;
    }

    public Resultado Decidir(bool ficar)
    {
        if (Fase != FaseDaMao.Decisao)
            return Resultado.Falha(ChavesDeErro.AcaoForaDeHora);
        var jogador = _ordem[_indice];
        if (ficar && !PodeFicar(jogador))
            return Resultado.Falha(ChavesDeErro.FichasInsuficientes);
        if (!ficar && _estados[jogador].BausPegos > 0)
            return Resultado.Falha(ChavesDeErro.ObrigadoAFicar);

        _estados[jogador].Ficou = ficar;
        _estados[jogador].Decisao = ficar ? DecisaoNaMao.Ficou : DecisaoNaMao.Correu;
        _estados[jogador].Decidiu = true;
        AvancarDecisao();
        return Resultado.Ok;
    }

    public Resultado PegarBau(int numero)
    {
        if (Fase != FaseDaMao.Decisao)
            return Resultado.Falha(ChavesDeErro.AcaoForaDeHora);
        if (numero is < 0 or > 1)
            return Resultado.Falha(ChavesDeErro.BauInexistente);
        if (_donosDosBaus[numero] is not null)
            return Resultado.Falha(ChavesDeErro.BauJaPego);
        var jogador = _ordem[_indice];
        if (!PodeFicar(jogador))
            return Resultado.Falha(ChavesDeErro.FichasInsuficientes);

        var estado = _estados[jogador];
        if (estado.BausPegos > 0)
        {
            if (!PodePagarTaxa(jogador))
                return Resultado.Falha(ChavesDeErro.TaxaDoBauSemFichas);
            jogador.Fichas -= ConfiguracaoDoJogo.TaxaDoSegundoBau;
            Montante += ConfiguracaoDoJogo.TaxaDoSegundoBau;
            _taxasDosBaus[numero] = ConfiguracaoDoJogo.TaxaDoSegundoBau;
            _movimentos.Add(new Movimento(jogador, TipoDeMovimento.TaxaDoBau, -ConfiguracaoDoJogo.TaxaDoSegundoBau));
        }

        estado.Cartas = _baus[numero];
        estado.BausPegos++;
        _donosDosBaus[numero] = jogador;
        if (_donosDosBaus.All(dono => dono is not null))
        {
            estado.Ficou = true;
            estado.Decisao = DecisaoNaMao.Ficou;
            estado.Decidiu = true;
            AvancarDecisao();
        }
        return Resultado.Ok;
    }

    public Resultado TempoEsgotadoNaDecisao()
    {
        if (Fase != FaseDaMao.Decisao)
            return Resultado.Falha(ChavesDeErro.AcaoForaDeHora);
        var estado = _estados[_ordem[_indice]];
        estado.Ficou = estado.BausPegos > 0;
        estado.Decisao = estado.Ficou ? DecisaoNaMao.Ficou : DecisaoNaMao.Ausente;
        estado.Decidiu = true;
        AvancarDecisao();
        return Resultado.Ok;
    }

    public Resultado JogarCarta(Carta carta)
    {
        if (Fase != FaseDaMao.Rodadas)
            return Resultado.Falha(ChavesDeErro.AcaoForaDeHora);
        var jogador = JogadorDaVez!;
        if (!_estados[jogador].Cartas.Contains(carta))
            return Resultado.Falha(ChavesDeErro.CartaNaoEhSua);
        if (!CartasPermitidas().Contains(carta))
            return Resultado.Falha(ChavesDeErro.DeveSeguirONaipe);

        Registrar(jogador, carta, foiAutomatica: false);
        return Resultado.Ok;
    }

    /// <summary>Joga a carta mais fraca permitida, virada para baixo, e conta uma ausência.</summary>
    public Resultado TempoEsgotadoNaJogada()
    {
        if (Fase != FaseDaMao.Rodadas)
            return Resultado.Falha(ChavesDeErro.AcaoForaDeHora);
        var jogador = JogadorDaVez!;
        var carta = CartasPermitidas().MinBy(c => ComparadorDeCartas.Forca(c, Mania));
        _estados[jogador].Ausencias++;
        Registrar(jogador, carta, foiAutomatica: true);
        return Resultado.Ok;
    }

    // Depois de pagar, quem pega e todos que já ficaram precisam cobrir o novo montante.
    private bool PodePagarTaxa(Jogador jogador)
    {
        var novoMontante = Montante + ConfiguracaoDoJogo.TaxaDoSegundoBau;
        return jogador.Fichas - ConfiguracaoDoJogo.TaxaDoSegundoBau >= novoMontante
            && _estados.Where(e => e.Value.Ficou).All(e => e.Key.Fichas >= novoMontante);
    }

    private void AvancarDecisao()
    {
        _indice++;
        if (_indice == _ordem.Count)
            IniciarRodadas();
    }

    private void IniciarRodadas()
    {
        var quemFicou = _ordem.Where(j => _estados[j].Ficou).ToList();
        if (quemFicou.Count == 0)
        {
            Distribuidor.Fichas += Montante;
            _movimentos.Add(new Movimento(Distribuidor, TipoDeMovimento.MontanteDoDistribuidor, Montante));
            Concluir(DesfechoDaMao.TodosCorreram);
            return;
        }
        if (quemFicou.Count == 1)
        {
            quemFicou[0].Fichas += Montante;
            _movimentos.Add(new Movimento(quemFicou[0], TipoDeMovimento.PremioDoUnicoQueFicou, Montante));
            Concluir(DesfechoDaMao.UnicoQueFicou);
            return;
        }
        _sequencia = quemFicou;
        _rodada = 1;
        Fase = FaseDaMao.Rodadas;
    }

    private void Registrar(Jogador jogador, Carta carta, bool foiAutomatica)
    {
        _estados[jogador].Cartas.Remove(carta);
        _jogadas.Add(new Jogada(jogador, carta, foiAutomatica));
        if (_jogadas.Count == _sequencia.Count)
            EncerrarRodada();
    }

    private void EncerrarRodada()
    {
        var cartas = _jogadas.Select(j => j.Carta).ToList();
        var vencedor = _sequencia[ComparadorDeCartas.IndiceDaVencedora(cartas, Mania)];
        vencedor.Fichas += ValorDaRodada;
        _estados[vencedor].Vitorias++;
        _movimentos.Add(new Movimento(vencedor, TipoDeMovimento.PremioDaRodada, ValorDaRodada));
        _rodadas.Add(new RodadaDisputada(_rodada, _sequencia[0], vencedor, ValorDaRodada, _jogadas.ToList()));

        if (_rodada == ConfiguracaoDoJogo.RodadasPorMao)
        {
            ReporMontante();
            Concluir(DesfechoDaMao.Disputada);
            return;
        }
        _rodada++;
        var inicio = _sequencia.IndexOf(vencedor);
        _sequencia = _sequencia.Skip(inicio).Concat(_sequencia.Take(inicio)).ToList();
        _jogadas.Clear();
    }

    private void ReporMontante()
    {
        foreach (var jogador in _sequencia.Where(j => _estados[j].Vitorias == 0))
        {
            jogador.Fichas -= Montante;
            MontantePendente += Montante;
            _movimentos.Add(new Movimento(jogador, TipoDeMovimento.ReposicaoDoMontante, -Montante));
        }
    }

    private void Concluir(DesfechoDaMao desfecho)
    {
        Fase = FaseDaMao.Concluida;
        Desfecho = desfecho;
    }
}
