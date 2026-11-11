using System.Globalization;
using Bestia.Recursos;

namespace Bestia.Web.Idioma;

/// <summary>Idioma da requisição em andamento, definido pelo middleware.</summary>
public sealed class ContextoDeIdioma
{
    public string Atual { get; set; } = Idiomas.Padrao;
}

/// <summary>Nas Views: <c>@T[Textos.Menu_Sair]</c> ou <c>@T.Obter(Textos.X, arg)</c>.</summary>
public sealed class Tradutor(ITextos textos, ContextoDeIdioma contexto)
{
    public string this[string chave] => textos.Obter(chave, contexto.Atual);

    public string Idioma => contexto.Atual;

    public string Obter(string chave, params object[] argumentos) => textos.Obter(chave, contexto.Atual, argumentos);
}

public static class ResolvedorDeIdioma
{
    public const string NomeDoCookie = "bestia.idioma";

    /// <summary>Ordem: perfil salvo, cookie, cabeçalho Accept-Language e, por fim, o padrão.</summary>
    public static string Resolver(string? doPerfil, string? doCookie, string? acceptLanguage)
    {
        var tags = new List<string?> { doPerfil, doCookie };
        tags.AddRange((acceptLanguage ?? "").Split(',').Select(parte => parte.Split(';')[0].Trim()));
        return tags
            .Select(Suportado)
            .FirstOrDefault(idioma => idioma is not null) ?? Idiomas.Padrao;
    }

    private static string? Suportado(string? tag) =>
        string.IsNullOrWhiteSpace(tag)
            ? null
            : Idiomas.Suportados.FirstOrDefault(s => s.Split('-')[0].Equals(tag.Split('-')[0], StringComparison.OrdinalIgnoreCase));
}

public sealed class MiddlewareDeIdioma(RequestDelegate proximo)
{
    public async Task InvokeAsync(HttpContext http, ContextoDeIdioma contexto)
    {
        http.Request.Cookies.TryGetValue(ResolvedorDeIdioma.NomeDoCookie, out var doCookie);
        contexto.Atual = ResolvedorDeIdioma.Resolver(
            http.User.FindFirst("idioma")?.Value, doCookie, http.Request.Headers.AcceptLanguage.ToString());
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(contexto.Atual);
        await proximo(http);
    }
}
