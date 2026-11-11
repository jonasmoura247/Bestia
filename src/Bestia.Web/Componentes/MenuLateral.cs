using Bestia.Recursos;
using Microsoft.AspNetCore.Mvc;

namespace Bestia.Web.Componentes;

public sealed record ItemDeMenu(string Chave, string Url, string Icone, bool Ativo);

public sealed class MenuLateralViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var caminho = Request.Path.ToString();
        var emSala = caminho.StartsWith("/salas/", StringComparison.OrdinalIgnoreCase) && caminho != "/salas/lista";
        IReadOnlyList<ItemDeMenu> itens =
        [
            new(Textos.Menu_IniciarPartida, "/salas#criar", "▶", emSala),
            new(Textos.Menu_Salas, "/salas", "🚪", caminho.Equals("/salas", StringComparison.OrdinalIgnoreCase)),
            new(Textos.Menu_MeuPerfil, "/perfil", "👤", caminho.StartsWith("/perfil", StringComparison.OrdinalIgnoreCase)),
            new(Textos.Menu_Regras, "/regras", "📖", caminho.StartsWith("/regras", StringComparison.OrdinalIgnoreCase)),
            new(Textos.Menu_Configuracoes, "/configuracoes", "⚙", caminho.StartsWith("/configuracoes", StringComparison.OrdinalIgnoreCase)),
        ];
        return View(itens);
    }
}
