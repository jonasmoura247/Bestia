using System.Security.Claims;
using Bestia.Aplicacao.Contas;
using Bestia.Aplicacao.Partidas;

namespace Bestia.Web.Seguranca;

public static class Sessao
{
    public const string EsquemaDoCookie = "Cookies";
    public const string EsquemaExterno = "Externo";
    public const string Google = "Google";

    public static ClaimsPrincipal Criar(SessaoDeConta sessao) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, sessao.Id.ToString()),
                new Claim(ClaimTypes.Name, sessao.Apelido),
                new Claim("idioma", sessao.Idioma),
                new Claim("carimbo", sessao.Carimbo.ToString()),
            ],
            EsquemaDoCookie));

    public static Guid ContaId(this ClaimsPrincipal usuario) =>
        Guid.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static PerfilDoJogador ComoPerfil(this SessaoDeConta sessao) =>
        new(sessao.Id, sessao.Apelido, sessao.Cor, sessao.Idioma, sessao.EmailConfirmado);

    public static async Task<bool> EhValidaAsync(ContaServico contas, ClaimsPrincipal? usuario)
    {
        var id = usuario?.FindFirstValue(ClaimTypes.NameIdentifier);
        var carimbo = usuario?.FindFirstValue("carimbo");
        return Guid.TryParse(id, out var contaId)
            && Guid.TryParse(carimbo, out var carimboId)
            && await contas.SessaoValidaAsync(contaId, carimboId);
    }
}
