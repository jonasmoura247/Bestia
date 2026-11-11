using System.Security.Cryptography;

namespace Bestia.Dominio;

public interface ISorteador
{
    /// <summary>Número inteiro em [0, limiteExclusivo).</summary>
    int Sortear(int limiteExclusivo);
}

public sealed class SorteadorCriptografico : ISorteador
{
    public int Sortear(int limiteExclusivo) => RandomNumberGenerator.GetInt32(limiteExclusivo);
}

public static class ListaExtensoes
{
    public static void Embaralhar<T>(this IList<T> itens, ISorteador sorteador)
    {
        for (var i = itens.Count - 1; i > 0; i--)
        {
            var j = sorteador.Sortear(i + 1);
            (itens[i], itens[j]) = (itens[j], itens[i]);
        }
    }
}
