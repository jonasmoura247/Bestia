namespace Bestia.Aplicacao.Contratos;

public interface ICriptografia
{
    /// <summary>Cifra um dado pessoal para gravar em coluna `...Cifrado`.</summary>
    byte[] Cifrar(string texto);

    string Decifrar(byte[] cifrado);

    /// <summary>Índice cego (coluna `...Hash`): permite buscar e garantir unicidade sem decifrar.</summary>
    byte[] IndiceCego(string texto);

    /// <summary>Hash de token de uso único (confirmação de e-mail, redefinição de senha).</summary>
    byte[] HashDeToken(string token);
}
