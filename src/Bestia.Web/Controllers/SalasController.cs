using Bestia.Aplicacao.Contas;
using Bestia.Aplicacao.Partidas;
using Bestia.Dominio;
using Bestia.Web.Modelos;
using Bestia.Web.Seguranca;
using Microsoft.AspNetCore.Mvc;

namespace Bestia.Web.Controllers;

[Route("salas")]
public sealed class SalasController(MesaServico mesas, ContaServico contas) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? erro)
    {
        var conta = User.ContaId();
        var sessao = (await contas.ObterSessaoAsync(conta))!;
        return View(new LobbyModelo
        {
            Salas = mesas.ListarSalasPublicas(),
            EmailConfirmado = sessao.EmailConfirmado,
            Erro = erro,
            CodigoDaMinhaSala = mesas.CodigoDaSalaDe(conta),
        });
    }

    /// <summary>O lobby consulta esta lista a cada 5 segundos.</summary>
    [HttpGet("lista")]
    public IActionResult Lista() => Json(mesas.ListarSalasPublicas());

    [HttpPost("criar")]
    public async Task<IActionResult> Criar(CriarSalaModelo modelo)
    {
        var sessao = (await contas.ObterSessaoAsync(User.ContaId()))!;
        var resultado = await mesas.CriarSalaAsync(sessao.ComoPerfil(), modelo.Nome, modelo.Maximo, modelo.Privada);
        return resultado.Sucesso
            ? RedirectToAction(nameof(Sala), new { codigo = resultado.Valor })
            : RedirectToAction(nameof(Index), new { erro = resultado.ChaveDoErro });
    }

    [HttpPost("entrar")]
    public Task<IActionResult> Entrar(EntrarNaSalaModelo modelo) => EntrarEIrAsync(modelo.Codigo);

    [HttpGet("{codigo}")]
    public Task<IActionResult> Sala(string codigo) => EntrarEIrAsync(codigo, mostrar: true);

    private async Task<IActionResult> EntrarEIrAsync(string codigo, bool mostrar = false)
    {
        var conta = User.ContaId();
        var codigoNormalizado = codigo.Trim().ToUpperInvariant();
        if (mesas.CodigoDaSalaDe(conta) != codigoNormalizado)
        {
            var sessao = (await contas.ObterSessaoAsync(conta))!;
            var entrada = await mesas.EntrarAsync(sessao.ComoPerfil(), codigoNormalizado);
            if (!entrada.Sucesso)
                return RedirectToAction(nameof(Index), new { erro = entrada.ChaveDoErro });
        }

        if (!mostrar)
            return RedirectToAction(nameof(Sala), new { codigo = codigoNormalizado });
        ViewData["Codigo"] = codigoNormalizado;
        return View(nameof(Sala));
    }
}
