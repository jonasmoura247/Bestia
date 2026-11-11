using System.Globalization;
using System.Text.Json;

namespace Bestia.Recursos;

public interface ITextos
{
    /// <summary>Texto da chave no idioma pedido; {0}, {1}... são preenchidos com os argumentos.</summary>
    string Obter(string chave, string? idioma, params object[] argumentos);
}

/// <summary>Lê o arquivo mestre embutido: uma chave, três idiomas lado a lado.</summary>
public sealed class ProvedorDeTextos : ITextos
{
    public static readonly IReadOnlyDictionary<string, Dictionary<string, string>> Mestre = Carregar();

    public string Obter(string chave, string? idioma, params object[] argumentos)
    {
        // Chave desconhecida (por exemplo vinda de um parâmetro da URL) volta como está, sem derrubar a página.
        if (!Mestre.TryGetValue(chave, out var traducoes))
            return chave;
        var modelo = traducoes[Idiomas.Normalizar(idioma)];
        return argumentos.Length == 0 ? modelo : string.Format(CultureInfo.InvariantCulture, modelo, argumentos);
    }

    private static Dictionary<string, Dictionary<string, string>> Carregar()
    {
        using var fluxo = typeof(ProvedorDeTextos).Assembly.GetManifestResourceStream("Bestia.Recursos.textos-mestre.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(fluxo)!;
    }
}
