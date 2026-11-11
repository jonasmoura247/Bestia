using System.Linq.Expressions;

namespace Bestia.Aplicacao.Contratos;

/// <summary>Porta de persistência genérica: o EF Core a implementa; os testes usam uma lista em memória.</summary>
public interface IRepositorio
{
    Task<T?> BuscarAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct = default) where T : class;

    Task<List<T>> ListarAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct = default) where T : class;

    void Adicionar<T>(T entidade) where T : class;

    void Remover<T>(T entidade) where T : class;

    Task SalvarAsync(CancellationToken ct = default);
}

public interface IRelogio
{
    DateTime AgoraUtc { get; }

    Task EsperarAsync(TimeSpan duracao, CancellationToken ct = default);
}

public interface IHashDeSenha
{
    string Gerar(string senha);

    bool Conferir(string hash, string senha);
}

public interface IEnviadorDeEmail
{
    Task EnviarAsync(string para, string assunto, string corpo, CancellationToken ct = default);
}

public interface IVerificadorDeBanco
{
    Task<bool> EstaDisponivelAsync(CancellationToken ct = default);
}
