using Bestia.Aplicacao.Contratos;
using Bestia.Aplicacao.Partidas;
using Bestia.Dominio;

namespace Bestia.Testes.Aplicacao;

internal sealed class SorteadorComSemente(int semente) : ISorteador
{
    private readonly Random _aleatorio = new(semente);

    public int Sortear(int limiteExclusivo) => _aleatorio.Next(limiteExclusivo);
}

/// <summary>Devolve primeiro os valores do roteiro (limitados ao intervalo) e depois cai no sorteio com semente.</summary>
internal sealed class SorteadorRoteirizado(IEnumerable<int> roteiro, int semente = 1) : ISorteador
{
    private readonly Queue<int> _roteiro = new(roteiro);
    private readonly Random _aleatorio = new(semente);

    public int Sortear(int limiteExclusivo) =>
        _roteiro.Count > 0 ? _roteiro.Dequeue() % limiteExclusivo : _aleatorio.Next(limiteExclusivo);
}

internal sealed class NotificadorFalso : INotificadorDaMesa
{
    public Dictionary<Guid, VisaoDaMesa> Ultimas { get; } = [];

    public List<(Guid Conta, VisaoDaMesa Visao)> Todas { get; } = [];

    public List<(Guid Conta, int Assento, int Emote)> Emotes { get; } = [];

    public bool Falhar { get; set; }

    public Task MesaAtualizadaAsync(Guid contaId, VisaoDaMesa visao)
    {
        if (Falhar)
            throw new InvalidOperationException("notificador fora do ar");
        lock (Todas)
        {
            Ultimas[contaId] = visao;
            Todas.Add((contaId, visao));
        }
        return Task.CompletedTask;
    }

    public Task EmoteAsync(Guid contaId, int assento, int emote)
    {
        if (Falhar)
            throw new InvalidOperationException("notificador fora do ar");
        lock (Emotes)
            Emotes.Add((contaId, assento, emote));
        return Task.CompletedTask;
    }
}

internal sealed class UnidadeDeTrabalhoFalsa(IRepositorio repositorio) : IUnidadeDeTrabalho
{
    public int ChamadasAteFalhar { get; set; } = int.MaxValue;

    public int Chamadas { get; private set; }

    public Task ExecutarAsync(Func<IRepositorio, Task> trabalho, CancellationToken ct = default)
    {
        Chamadas++;
        if (Chamadas > ChamadasAteFalhar)
            throw new InvalidOperationException("banco fora do ar");
        return trabalho(repositorio);
    }
}
