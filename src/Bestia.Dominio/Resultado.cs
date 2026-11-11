namespace Bestia.Dominio;

/// <summary>Falha esperada de regra: carrega a chave do resource, nunca o texto.</summary>
public readonly record struct Resultado(string? ChaveDoErro)
{
    public static Resultado Ok => default;

    public bool Sucesso => ChaveDoErro is null;

    public static Resultado Falha(string chaveDoErro) => new(chaveDoErro);
}

/// <summary>Resultado que, em caso de sucesso, devolve um valor.</summary>
public readonly record struct Resultado<T>(T? Valor, string? ChaveDoErro)
{
    public bool Sucesso => ChaveDoErro is null;

    public static Resultado<T> Ok(T valor) => new(valor, null);

    public static Resultado<T> Falha(string chaveDoErro) => new(default, chaveDoErro);
}

public static class ChavesDeErro
{
    public const string AcaoForaDeHora = "Erro_AcaoForaDeHora";
    public const string CartaNaoEhSua = "Erro_CartaNaoEhSua";
    public const string DeveSeguirONaipe = "Erro_DeveSeguirONaipe";
    public const string FichasInsuficientes = "Erro_FichasInsuficientes";
    public const string ObrigadoAFicar = "Erro_ObrigadoAFicar";
    public const string BauInexistente = "Erro_BauInexistente";
    public const string BauJaPego = "Erro_BauJaPego";
    public const string TaxaDoBauSemFichas = "Erro_TaxaDoBauSemFichas";
    public const string JogadorForaDaPartida = "Erro_JogadorForaDaPartida";
    public const string ProximoDistribuidorNaoPodeSair = "Erro_ProximoDistribuidorNaoPodeSair";
    public const string AceiteObrigatorio = "Erro_AceiteObrigatorio";
    public const string MaioridadeObrigatoria = "Erro_MaioridadeObrigatoria";
    public const string ApelidoInvalido = "Erro_ApelidoInvalido";
    public const string EmailInvalido = "Erro_EmailInvalido";
    public const string SenhaFraca = "Erro_SenhaFraca";
    public const string EmailEmUso = "Erro_EmailEmUso";
    public const string ApelidoEmUso = "Erro_ApelidoEmUso";
    public const string TokenInvalido = "Erro_TokenInvalido";
    public const string CredenciaisInvalidas = "Erro_CredenciaisInvalidas";
    public const string GoogleEmailNaoVerificado = "Erro_GoogleEmailNaoVerificado";
    public const string SenhaIncorreta = "Erro_SenhaIncorreta";
    public const string ContaNaoEncontrada = "Erro_ContaNaoEncontrada";
    public const string EmailNaoConfirmado = "Erro_EmailNaoConfirmado";
    public const string NaoEhSuaVez = "Erro_NaoEhSuaVez";
    public const string SalaNaoEncontrada = "Erro_SalaNaoEncontrada";
    public const string SalaCheia = "Erro_SalaCheia";
    public const string SalaInvalida = "Erro_SalaInvalida";
    public const string JaEstaEmSala = "Erro_JaEstaEmSala";
    public const string SomenteOCriador = "Erro_SomenteOCriador";
    public const string JogadoresInsuficientes = "Erro_JogadoresInsuficientes";
    public const string PartidaEmAndamento = "Erro_PartidaEmAndamento";
    public const string EmoteMuitoRapido = "Erro_EmoteMuitoRapido";
    public const string NaoEstaEmSala = "Erro_NaoEstaEmSala";
    public const string OperacaoRequerSenha = "Erro_OperacaoRequerSenha";
}
