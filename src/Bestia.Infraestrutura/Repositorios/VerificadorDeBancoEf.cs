using Bestia.Aplicacao.Contratos;
using Bestia.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Bestia.Infraestrutura.Repositorios;

public sealed class VerificadorDeBancoEf(IDbContextFactory<BestiaDbContext> fabrica) : IVerificadorDeBanco
{
    public async Task<bool> EstaDisponivelAsync(CancellationToken ct = default)
    {
        await using var banco = await fabrica.CreateDbContextAsync(ct);
        return await banco.Database.CanConnectAsync(ct);
    }
}
