using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Bestia.Aplicacao.Contratos;

namespace Bestia.Testes.Aplicacao;

/// <summary>Lista em memória que imita o EF: só enxerga o que já foi salvo e compara byte[] por conteúdo.</summary>
internal sealed class RepositorioEmMemoria : IRepositorio
{
    private readonly List<object> _salvos = [];
    private readonly List<object> _pendentes = [];

    public int Salvamentos { get; private set; }

    public IEnumerable<T> Todos<T>() => _salvos.Concat(_pendentes).OfType<T>();

    public Task<T?> BuscarAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct = default) where T : class =>
        Task.FromResult(_salvos.OfType<T>().FirstOrDefault(Compilar(filtro)));

    public Task<List<T>> ListarAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct = default) where T : class =>
        Task.FromResult(_salvos.OfType<T>().Where(Compilar(filtro)).ToList());

    public void Adicionar<T>(T entidade) where T : class => _pendentes.Add(entidade);

    public void Remover<T>(T entidade) where T : class
    {
        _salvos.Remove(entidade);
        _pendentes.Remove(entidade);
    }

    public Task SalvarAsync(CancellationToken ct = default)
    {
        _salvos.AddRange(_pendentes);
        _pendentes.Clear();
        Salvamentos++;
        return Task.CompletedTask;
    }

    public static bool BytesIguais(byte[]? a, byte[]? b) => a is not null && b is not null && a.AsSpan().SequenceEqual(b);

    private static Func<T, bool> Compilar<T>(Expression<Func<T, bool>> filtro) =>
        ((Expression<Func<T, bool>>)new TrocaIgualdadeDeBytes().Visit(filtro)).Compile();

    private sealed class TrocaIgualdadeDeBytes : ExpressionVisitor
    {
        protected override Expression VisitBinary(BinaryExpression no)
        {
            if (no is { NodeType: ExpressionType.Equal, Left.Type: var tipo } && tipo == typeof(byte[]))
                return Expression.Call(typeof(RepositorioEmMemoria).GetMethod(nameof(BytesIguais))!, Visit(no.Left), Visit(no.Right));
            return base.VisitBinary(no);
        }
    }
}

internal sealed class RelogioFalso : IRelogio
{
    private readonly object _trava = new();
    private readonly List<(DateTime Limite, TaskCompletionSource Fim)> _esperas = [];
    private DateTime _agora = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    public DateTime AgoraUtc
    {
        get
        {
            lock (_trava)
                return _agora;
        }
    }

    public Task EsperarAsync(TimeSpan duracao, CancellationToken ct = default)
    {
        var fim = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_trava)
            _esperas.Add((_agora + duracao, fim));
        ct.Register(() => fim.TrySetCanceled(ct));
        return fim.Task;
    }

    public int Esperas
    {
        get
        {
            lock (_trava)
                return _esperas.Count(e => !e.Fim.Task.IsCompleted);
        }
    }

    public void Avancar(TimeSpan tempo)
    {
        List<(DateTime Limite, TaskCompletionSource Fim)> vencidas;
        lock (_trava)
        {
            _agora += tempo;
            vencidas = _esperas.Where(e => e.Limite <= _agora).ToList();
            _esperas.RemoveAll(e => e.Limite <= _agora);
        }
        foreach (var espera in vencidas)
            espera.Fim.TrySetResult();
    }
}

internal sealed class HashDeSenhaFalso : IHashDeSenha
{
    public string Gerar(string senha) => "H:" + senha;

    public bool Conferir(string hash, string senha) => hash == "H:" + senha;
}

internal sealed class EnviadorDeEmailFalso : IEnviadorDeEmail
{
    public List<(string Para, string Assunto, string Corpo)> Enviados { get; } = [];

    public Task EnviarAsync(string para, string assunto, string corpo, CancellationToken ct = default)
    {
        Enviados.Add((para, assunto, corpo));
        return Task.CompletedTask;
    }
}

internal sealed class CriptografiaFalsa : ICriptografia
{
    public byte[] Cifrar(string texto) => [0xAA, .. Encoding.UTF8.GetBytes(texto)];

    public string Decifrar(byte[] cifrado) => Encoding.UTF8.GetString(cifrado, 1, cifrado.Length - 1);

    public byte[] IndiceCego(string texto) => SHA256.HashData(Encoding.UTF8.GetBytes("i:" + texto.Trim().ToLowerInvariant()));

    public byte[] HashDeToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes("t:" + token));
}
