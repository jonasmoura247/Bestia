using Bestia.Aplicacao.Perfil;
using Bestia.Aplicacao.Partidas;

namespace Bestia.Web.Modelos;

public sealed class EntrarModelo
{
    public string Email { get; set; } = "";
    public string Senha { get; set; } = "";
    public string? RetornoUrl { get; set; }
}

public sealed class CriarContaModelo
{
    public string Apelido { get; set; } = "";
    public string Email { get; set; } = "";
    public string Senha { get; set; } = "";
    public bool AceitoTermos { get; set; }
    public bool DeclaroMaioridade { get; set; }
}

public sealed class EsqueciSenhaModelo
{
    public string Email { get; set; } = "";
}

public sealed class RedefinirSenhaModelo
{
    public string Token { get; set; } = "";
    public string NovaSenha { get; set; } = "";
}

public sealed class TermosGoogleModelo
{
    public string Apelido { get; set; } = "";
    public bool AceitoTermos { get; set; }
    public bool DeclaroMaioridade { get; set; }
}

public sealed class CriarSalaModelo
{
    public string Nome { get; set; } = "";
    public int Maximo { get; set; } = 6;
    public bool Privada { get; set; }
}

public sealed class EntrarNaSalaModelo
{
    public string Codigo { get; set; } = "";
}

public sealed class LobbyModelo
{
    public IReadOnlyList<ResumoDaSala> Salas { get; init; } = [];
    public bool EmailConfirmado { get; init; }
    public string? Erro { get; init; }
    public string? CodigoDaMinhaSala { get; init; }
}

public sealed class PerfilModelo
{
    public string Apelido { get; init; } = "";
    public EstatisticasDoPerfil Estatisticas { get; init; } = new(0, 0, 0, []);
}
