using System.Security.Claims;
using Bestia.Aplicacao.Contas;
using Bestia.Web.Idioma;
using Bestia.Web.Modelos;
using Bestia.Web.Seguranca;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bestia.Web.Controllers;

[AllowAnonymous]
[Route("conta")]
public sealed class ContaController(
    ContaServico contas, ContextoDeIdioma idioma, IAuthenticationSchemeProvider esquemas) : Controller
{
    private string Ip => $"{HttpContext.Connection.RemoteIpAddress}";

    [HttpGet("entrar")]
    public IActionResult Entrar(string? retornoUrl, string? aviso) =>
        View(new EntrarModelo { RetornoUrl = retornoUrl }.ComAviso(this, aviso));

    [HttpPost("entrar")]
    [EnableRateLimiting("conta")]
    public async Task<IActionResult> Entrar(EntrarModelo modelo)
    {
        var resultado = await contas.EntrarAsync(modelo.Email, modelo.Senha, Ip);
        if (!resultado.Sucesso)
        {
            ViewData["Erro"] = resultado.ChaveDoErro;
            modelo.Senha = "";
            return View(modelo);
        }

        await HttpContext.SignInAsync(Sessao.EsquemaDoCookie, Sessao.Criar(resultado.Valor!));
        return Destino(modelo.RetornoUrl);
    }

    [HttpGet("criar")]
    public IActionResult Criar() => View(new CriarContaModelo());

    [HttpPost("criar")]
    [EnableRateLimiting("conta")]
    public async Task<IActionResult> Criar(CriarContaModelo modelo)
    {
        var pedido = new PedidoDeCadastro(
            modelo.Apelido, modelo.Email, modelo.Senha, modelo.AceitoTermos, modelo.DeclaroMaioridade,
            idioma.Atual, Ip, token => Url.Action(nameof(Confirmar), "Conta", new { token }, Request.Scheme)!);
        var resultado = await contas.CriarContaAsync(pedido);
        if (!resultado.Sucesso)
        {
            ViewData["Erro"] = resultado.ChaveDoErro;
            modelo.Senha = "";
            return View(modelo);
        }
        return RedirectToAction(nameof(Entrar), new { aviso = "cadastro" });
    }

    [HttpGet("confirmar")]
    public async Task<IActionResult> Confirmar(string token)
    {
        var resultado = await contas.ConfirmarEmailAsync(token);
        ViewData["Erro"] = resultado.ChaveDoErro;
        return View();
    }

    [HttpGet("esqueci-senha")]
    public IActionResult EsqueciSenha() => View(new EsqueciSenhaModelo());

    [HttpPost("esqueci-senha")]
    [EnableRateLimiting("conta")]
    public async Task<IActionResult> EsqueciSenha(EsqueciSenhaModelo modelo)
    {
        await contas.EsqueciSenhaAsync(
            modelo.Email, token => Url.Action(nameof(RedefinirSenha), "Conta", new { token }, Request.Scheme)!);
        ViewData["Enviado"] = true;
        return View(new EsqueciSenhaModelo());
    }

    [HttpGet("redefinir-senha")]
    public IActionResult RedefinirSenha(string token) => View(new RedefinirSenhaModelo { Token = token });

    [HttpPost("redefinir-senha")]
    [EnableRateLimiting("conta")]
    public async Task<IActionResult> RedefinirSenha(RedefinirSenhaModelo modelo)
    {
        var resultado = await contas.RedefinirSenhaAsync(modelo.Token, modelo.NovaSenha);
        if (!resultado.Sucesso)
        {
            ViewData["Erro"] = resultado.ChaveDoErro;
            modelo.NovaSenha = "";
            return View(modelo);
        }
        return RedirectToAction(nameof(Entrar), new { aviso = "senha" });
    }

    [Authorize]
    [HttpPost("sair")]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(Sessao.EsquemaDoCookie);
        return RedirectToAction(nameof(Entrar));
    }

    [HttpPost("idioma")]
    public async Task<IActionResult> Idioma(string idioma, string? retornoUrl)
    {
        var escolhido = Bestia.Recursos.Idiomas.Normalizar(idioma);
        Response.Cookies.Append(ResolvedorDeIdioma.NomeDoCookie, escolhido, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(365),
            IsEssential = true,
        });

        if (User.Identity!.IsAuthenticated)
        {
            var conta = User.ContaId();
            await contas.AlterarIdiomaAsync(conta, escolhido);
            var sessao = await contas.ObterSessaoAsync(conta);
            await HttpContext.SignInAsync(Sessao.EsquemaDoCookie, Sessao.Criar(sessao!));
        }
        return Destino(retornoUrl);
    }

    // ---------- Google ----------

    [HttpGet("google")]
    public async Task<IActionResult> Google()
    {
        if (await esquemas.GetSchemeAsync(Sessao.Google) is null)
            return NotFound();
        var propriedades = new AuthenticationProperties { RedirectUri = Url.Action(nameof(GoogleRetorno)) };
        return Challenge(propriedades, Sessao.Google);
    }

    [HttpGet("google-retorno")]
    public async Task<IActionResult> GoogleRetorno()
    {
        var externo = await HttpContext.AuthenticateAsync(Sessao.EsquemaExterno);
        if (!externo.Succeeded)
            return RedirectToAction(nameof(Entrar));

        var resultado = await contas.EntrarComGoogleAsync(IdentidadeDe(externo.Principal!), Ip);
        if (resultado.PrecisaDoCadastro)
            return RedirectToAction(nameof(Termos));

        await HttpContext.SignOutAsync(Sessao.EsquemaExterno);
        if (resultado.Sessao is null)
        {
            ViewData["Erro"] = resultado.ChaveDoErro;
            return View(nameof(Entrar), new EntrarModelo());
        }
        await HttpContext.SignInAsync(Sessao.EsquemaDoCookie, Sessao.Criar(resultado.Sessao));
        return Destino(null);
    }

    [HttpGet("termos")]
    public async Task<IActionResult> Termos()
    {
        var externo = await HttpContext.AuthenticateAsync(Sessao.EsquemaExterno);
        if (!externo.Succeeded)
            return RedirectToAction(nameof(Entrar));
        return View(new TermosGoogleModelo { Apelido = externo.Principal!.FindFirstValue(ClaimTypes.Name) ?? "" });
    }

    [HttpPost("termos")]
    [EnableRateLimiting("conta")]
    public async Task<IActionResult> Termos(TermosGoogleModelo modelo)
    {
        var externo = await HttpContext.AuthenticateAsync(Sessao.EsquemaExterno);
        if (!externo.Succeeded)
            return RedirectToAction(nameof(Entrar));

        var resultado = await contas.ConcluirCadastroComGoogleAsync(
            IdentidadeDe(externo.Principal!), modelo.Apelido, modelo.AceitoTermos, modelo.DeclaroMaioridade, idioma.Atual, Ip);
        if (!resultado.Sucesso)
        {
            ViewData["Erro"] = resultado.ChaveDoErro;
            return View(modelo);
        }

        await HttpContext.SignOutAsync(Sessao.EsquemaExterno);
        await HttpContext.SignInAsync(Sessao.EsquemaDoCookie, Sessao.Criar(resultado.Valor!));
        return Destino(null);
    }

    private static IdentidadeGoogle IdentidadeDe(ClaimsPrincipal usuario) =>
        new(
            usuario.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
            usuario.FindFirstValue(ClaimTypes.Email) ?? "",
            string.Equals(usuario.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase),
            usuario.FindFirstValue(ClaimTypes.Name));

    private IActionResult Destino(string? retornoUrl) =>
        !string.IsNullOrEmpty(retornoUrl) && Url.IsLocalUrl(retornoUrl) ? LocalRedirect(retornoUrl) : Redirect("/salas");
}

internal static class ModeloDeEntrarExtensoes
{
    public static EntrarModelo ComAviso(this EntrarModelo modelo, Controller controller, string? aviso)
    {
        controller.ViewData["Aviso"] = aviso;
        return modelo;
    }
}
