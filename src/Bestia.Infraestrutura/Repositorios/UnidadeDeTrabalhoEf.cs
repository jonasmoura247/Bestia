using Bestia.Aplicacao.Contratos;
using Bestia.Aplicacao.Partidas;
using Bestia.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Bestia.Infraestrutura.Repositorios;

/// <summary>Um contexto novo por trabalho: serve a serviços singleton (como a mesa ao vivo).</summary>
public sealed class UnidadeDeTrabalhoEf(IDbContextFactory<BestiaDbContext> fabrica) : IUnidadeDeTrabalho
{
    public async Task ExecutarAsync(Func<IRepositorio, Task> trabalho, CancellationToken ct = default)
    {
        await using var banco = await fabrica.CreateDbContextAsync(ct);
        await trabalho(new RepositorioEf(banco));
    }
}
