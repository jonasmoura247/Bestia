using System.Security.Claims;
using System.Text;
using Bestia.Aplicacao.Contas;
using Bestia.Aplicacao.Modelos;
using Bestia.Aplicacao.Partidas;
using Bestia.Aplicacao.Privacidade;
using Bestia.Web.Seguranca;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Bestia.Web.Controllers;

[Route("configuracoes/privacidade")]
public sealed class PrivacidadeController(
    PrivacidadeServico privacidade, ContaServico contas, MesaServico mesas, IAuthenticationSchemeProvider esquemas) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? ok, string? erro)
    {
        ViewData["Ok"] = ok;
        ViewData["Erro"] = erro;
        var dados = await privacidade.MeusDadosAsync(User.ContaId());
        ViewData["GoogleAtivo"] = await esquemas.GetSchemeAsync(Sessao.Google) is not null;
        return View(dados);
    }

    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar()
    {
        var resultado = await privacidade.ExportarAsync(User.ContaId());
        return File(Encoding.UTF8.GetBytes(resultado.Valor!), "application/json", "meus-dados-bestia.json");
    }

    [HttpPost("apelido")]
    public async Task<IActionResult> Apelido(string novoApelido)
    {
        var resultado = await privacidade.AlterarApelidoAsync(User.ContaId(), novoApelido);
        if (resultado.Sucesso)
            await AtualizarCookieAsync();
        return Voltar(resultado, "apelido");
    }

    [HttpPost("email")]
    public async Task<IActionResult> Email(string novoEmail, string senha)
    {
        var resultado = await privacidade.AlterarEmailAsync(
            User.ContaId(), novoEmail, senha, token => Url.Action("Confirmar", "Conta", new { token }, Request.Scheme)!);
        return Voltar(resultado, "email");
    }

    [HttpPost("solicitar")]
    public async Task<IActionResult> Solicitar(TipoDeSolicitacao tipo)
    {
        var resultado = await privacidade.SolicitarAoEncarregadoAsync(User.ContaId(), tipo);
        return Voltar(resultado, "solicitacao");
    }

    [HttpPost("revogar")]
    public async Task<IActionResult> Revogar(string documento)
    {
        var conta = User.ContaId();
        await mesas.SairAsync(conta);
        var resultado = await privacidade.RevogarConsentimentoAsync(conta, documento);
        return await EncerrarSessaoAsync(resultado);
    }

    [HttpPost("excluir")]
    public async Task<IActionResult> Excluir(string senha)
    {
        var conta = User.ContaId();
        var resultado = await privacidade.ExcluirContaAsync(conta, senha, reautenticouComGoogle: false);
        if (resultado.Sucesso)
            await mesas.SairAsync(conta);
        return await EncerrarSessaoAsync(resultado);
    }

    [HttpGet("excluir-google")]
    public async Task<IActionResult> ExcluirComGoogle()
    {
        if (await esquemas.GetSchemeAsync(Sessao.Google) is null)
            return NotFound();
        var propriedades = new AuthenticationProperties { RedirectUri = Url.Action(nameof(ExcluirComGoogleRetorno)) };
        return Challenge(propriedades, Sessao.Google);
    }

    [HttpGet("excluir-google-retorno")]
    public async Task<IActionResult> ExcluirComGoogleRetorno()
    {
        var externo = await HttpContext.AuthenticateAsync(Sessao.EsquemaExterno);
        var conta = User.ContaId();
        var idGoogle = externo.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        await HttpContext.SignOutAsync(Sessao.EsquemaExterno);

        var confirmou = externo.Succeeded && await privacidade.ConfirmarIdentidadeGoogleAsync(conta, idGoogle);
        var resultado = await privacidade.ExcluirContaAsync(conta, null, confirmou);
        if (resultado.Sucesso)
            await mesas.SairAsync(conta);
        return await EncerrarSessaoAsync(resultado);
    }

    private IActionResult Voltar(Bestia.Dominio.Resultado resultado, string ok) =>
        resultado.Sucesso
            ? RedirectToAction(nameof(Index), new { ok })
            : RedirectToAction(nameof(Index), new { erro = resultado.ChaveDoErro });

    private async Task<IActionResult> EncerrarSessaoAsync(Bestia.Dominio.Resultado resultado)
    {
        if (!resultado.Sucesso)
            return RedirectToAction(nameof(Index), new { erro = resultado.ChaveDoErro });
        await HttpContext.SignOutAsync(Sessao.EsquemaDoCookie);
        return Redirect("/conta/entrar?aviso=excluida");
    }

    private async Task AtualizarCookieAsync()
    {
        var sessao = await contas.ObterSessaoAsync(User.ContaId());
        await HttpContext.SignInAsync(Sessao.EsquemaDoCookie, Sessao.Criar(sessao!));
    }
}
