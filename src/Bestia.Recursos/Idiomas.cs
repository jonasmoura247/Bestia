namespace Bestia.Recursos;

public static class Idiomas
{
    public const string Padrao = "pt-BR";

    public static readonly IReadOnlyList<string> Suportados = ["pt-BR", "en", "es"];

    /// <summary>Devolve um idioma suportado; qualquer outro valor vira o padrão.</summary>
    public static string Normalizar(string? idioma) =>
        Suportados.FirstOrDefault(s => string.Equals(s, idioma, StringComparison.OrdinalIgnoreCase)) ?? Padrao;
}
