using Bestia.Dominio.Cartas;
using Bestia.Dominio.Jogadores;

namespace Bestia.Dominio.Partidas;

public sealed class Partida
{
    private readonly ISorteador _sorteador;
    private readonly HashSet<Jogador> _vaoDesistir = [];
    private int _indiceDoDistribuidor;

    public Partida(IEnumerable<Jogador> jogadores, ISorteador sorteador)
    {
        var lista = jogadores.ToList();
        if (lista.Count is < ConfiguracaoDoJogo.MinimoDeJogadores or > ConfiguracaoDoJogo.MaximoDeJogadores)
            throw new ArgumentOutOfRangeException(nameof(jogadores));

        lista.Embaralhar(sorteador);
        Assentos = lista;
        _sorteador = sorteador;
        _indiceDoDistribuidor = sorteador.Sortear(lista.Count);
    }

    /// <summary>Em sentido horário; o índice 0 é o assento das 12 h.</summary>
    public IReadOnlyList<Jogador> Assentos { get; }

    public Mao? MaoAtual { get; private set; }

    public int MontantePendente { get; private set; }

    public Jogador Distribuidor => Assentos[_indiceDoDistribuidor];

    public IReadOnlyList<Jogador> Vivos => Assentos.Where(j => j.EstaNaPartida).ToList();

    public bool Encerrada => Vivos.Count < ConfiguracaoDoJogo.MinimoDeJogadores;

    public IEnumerable<Jogador> Ranking => Assentos.OrderByDescending(j => j.Fichas);

    public Mao IniciarMao()
    {
        if (Encerrada || MaoAtual is not null)
            throw new InvalidOperationException("Não há como iniciar uma mão agora.");

        var baralho = Baralho.Criar().ToList();
        baralho.Embaralhar(_sorteador);
        MaoAtual = new Mao(Vivos, Distribuidor, MontantePendente, baralho);
        return MaoAtual;
    }

    public void EncerrarMao()
    {
        if (MaoAtual is not { Fase: FaseDaMao.Concluida } mao)
            throw new InvalidOperationException("A mão ainda não terminou.");

        MontantePendente = mao.MontantePendente;
        foreach (var jogador in Vivos)
        {
            if (mao.Ausencias(jogador) >= ConfiguracaoDoJogo.AusenciasParaEliminar)
                Remover(jogador, MotivoDaSaida.Inatividade);
            else if (jogador.Fichas < ConfiguracaoDoJogo.MinimoDeFichasParaContinuar)
                Remover(jogador, MotivoDaSaida.SemFichas);
            else if (_vaoDesistir.Contains(jogador))
                Remover(jogador, MotivoDaSaida.Desistencia);
        }
        _indiceDoDistribuidor = IndiceDoProximoVivoApos(_indiceDoDistribuidor);
        MaoAtual = null;
    }

    /// <summary>Sai da partida ao fim da mão (ou na hora, entre mãos). O próximo distribuidor não pode sair.</summary>
    public Resultado Desistir(Jogador jogador)
    {
        if (!jogador.EstaNaPartida)
            return Resultado.Falha(ChavesDeErro.JogadorForaDaPartida);
        var proximoDistribuidor = MaoAtual is null ? Distribuidor : Assentos[IndiceDoProximoVivoApos(_indiceDoDistribuidor)];
        if (jogador == proximoDistribuidor)
            return Resultado.Falha(ChavesDeErro.ProximoDistribuidorNaoPodeSair);

        if (MaoAtual is null)
            Remover(jogador, MotivoDaSaida.Desistencia);
        else
            _vaoDesistir.Add(jogador);
        return Resultado.Ok;
    }

    private static void Remover(Jogador jogador, MotivoDaSaida motivo)
    {
        jogador.EstaNaPartida = false;
        jogador.MotivoDaSaida = motivo;
    }

    private int IndiceDoProximoVivoApos(int indice) =>
        Enumerable.Range(1, Assentos.Count)
            .Select(passo => (indice + passo) % Assentos.Count)
            .FirstOrDefault(i => Assentos[i].EstaNaPartida, indice);
}
