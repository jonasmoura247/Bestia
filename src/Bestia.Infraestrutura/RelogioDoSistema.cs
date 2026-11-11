using Bestia.Aplicacao.Contratos;

namespace Bestia.Infraestrutura;

public sealed class RelogioDoSistema : IRelogio
{
    public DateTime AgoraUtc => DateTime.UtcNow;

    public Task EsperarAsync(TimeSpan duracao, CancellationToken ct = default) => Task.Delay(duracao, ct);
}
