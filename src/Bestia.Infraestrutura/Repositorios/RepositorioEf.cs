using System.Linq.Expressions;
using Bestia.Aplicacao.Contratos;
using Bestia.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Bestia.Infraestrutura.Repositorios;

public sealed class RepositorioEf(BestiaDbContext banco) : IRepositorio
{
    public Task<T?> BuscarAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct = default) where T : class =>
        banco.Set<T>().FirstOrDefaultAsync(filtro, ct);

    public Task<List<T>> ListarAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct = default) where T : class =>
        banco.Set<T>().Where(filtro).ToListAsync(ct);

    public void Adicionar<T>(T entidade) where T : class => banco.Set<T>().Add(entidade);

    public void Remover<T>(T entidade) where T : class => banco.Set<T>().Remove(entidade);

    public Task SalvarAsync(CancellationToken ct = default) => banco.SaveChangesAsync(ct);
}
