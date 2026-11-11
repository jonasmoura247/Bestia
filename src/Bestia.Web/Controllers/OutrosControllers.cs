using Bestia.Aplicacao.Contas;
using Bestia.Aplicacao.Perfil;
using Bestia.Recursos;
using Bestia.Web.Modelos;
using Bestia.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bestia.Web.Controllers;

[AllowAnonymous]
public sealed class InicioController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() =>
        User.Identity!.IsAuthenticated ? Redirect("/salas") : Redirect("/conta/entrar");
}

[AllowAnonymous]
[Route("regras")]
public sealed class RegrasController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}

[AllowAnonymous]
[Route("legal")]
public sealed class LegalController(IConfiguration configuracao) : Controller
{
    [HttpGet("termos")]
    public IActionResult Termos() => View();

    [HttpGet("privacidade")]
    public IActionResult Privacidade()
    {
        ViewData["EncarregadoEmail"] = configuracao["Privacidade:EncarregadoEmail"];
        return View();
    }
}

[Route("perfil")]
public sealed class PerfilController(PerfilServico perfis, ContaServico contas) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var conta = User.ContaId();
        var sessao = (await contas.ObterSessaoAsync(conta))!;
        return View(new PerfilModelo { Apelido = sessao.Apelido, Estatisticas = await perfis.ObterAsync(conta) });
    }
}

[Route("configuracoes")]
public sealed class ConfiguracoesController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}

[AllowAnonymous]
public sealed class ErroController : Controller
{
    [HttpGet("/erro")]
    [HttpPost("/erro")]
    public IActionResult Index(int? codigo)
    {
        var status = codigo ?? StatusCodes.Status500InternalServerError;
        Response.StatusCode = status;
        ViewData["Codigo"] = status;
        return View();
    }
}

[AllowAnonymous]
public sealed class RecursosController : Controller
{
    private static readonly string[] Prefixos = ["Partida_", "Sala_", "Erro_", "Emote_", "Comum_"];

    /// <summary>Textos que o JavaScript da mesa usa, vindos do mesmo arquivo mestre.</summary>
    [HttpGet("/recursos/{cultura}.json")]
    public IActionResult Textos(string cultura)
    {
        var idioma = Idiomas.Normalizar(cultura);
        var textos = ProvedorDeTextos.Mestre
            .Where(par => Prefixos.Any(prefixo => par.Key.StartsWith(prefixo, StringComparison.Ordinal)))
            .ToDictionary(par => par.Key, par => par.Value[idioma]);
        return Json(textos);
    }
}

/// <summary>Só em Development: lista os e-mails que o sistema "enviou", para clicar nos links de teste.</summary>
[AllowAnonymous]
public sealed class DesenvolvimentoController(IWebHostEnvironment ambiente, Bestia.Infraestrutura.Email.CaixaDeEmails caixa) : Controller
{
    private static bool HostLocal(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || (System.Net.IPAddress.TryParse(host, out var ip) && (System.Net.IPAddress.IsLoopback(ip) || IpPrivado(ip)));

    private static bool IpPrivado(System.Net.IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b.Length == 4 && (b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168));
    }

    [HttpGet("/dev/emails")]
    public IActionResult Emails()
    {
        // Só a própria máquina ou a rede local: nunca expor a caixa de e-mails por um túnel público (ngrok etc.).
        if (!ambiente.IsDevelopment() || Request.Headers.ContainsKey("X-Forwarded-For") || !HostLocal(Request.Host.Host))
            return NotFound();

        var itens = caixa.Listar().Select(m =>
        {
            var corpo = System.Text.RegularExpressions.Regex.Replace(
                System.Net.WebUtility.HtmlEncode(m.Corpo),
                @"https?://[^\s<]+",
                link => $"<a href=\"{link.Value}\">{link.Value}</a>");
            return $"<li><b>{System.Net.WebUtility.HtmlEncode(m.Assunto)}</b><br><small>para {System.Net.WebUtility.HtmlEncode(m.Para)} · {m.Em:HH:mm:ss}</small><p>{corpo}</p></li>";
        });
        var html = "<!doctype html><meta charset=\"utf-8\"><title>E-mails de desenvolvimento</title>" +
                   "<body style=\"font-family:sans-serif;max-width:800px;margin:2em auto\"><h1>E-mails de desenvolvimento</h1>" +
                   (caixa.Listar().Count == 0 ? "<p>Nenhum e-mail ainda.</p>" : $"<ul>{string.Join("", itens)}</ul>");
        return Content(html, "text/html; charset=utf-8");
    }
}
