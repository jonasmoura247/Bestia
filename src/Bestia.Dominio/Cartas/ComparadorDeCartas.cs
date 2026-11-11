namespace Bestia.Dominio.Cartas;

public static class ComparadorDeCartas
{
    /// <summary>Índice da carta vencedora; a primeira jogada define o naipe aberto.</summary>
    public static int IndiceDaVencedora(IReadOnlyList<Carta> jogadas, Naipe mania)
    {
        var naipeAberto = jogadas[0].Naipe;
        var vencedora = 0;
        for (var i = 1; i < jogadas.Count; i++)
        {
            if (Chave(jogadas[i], mania, naipeAberto).CompareTo(Chave(jogadas[vencedora], mania, naipeAberto)) > 0)
                vencedora = i;
        }
        return vencedora;
    }

    /// <summary>Força da carta ignorando o naipe aberto: mania pesa mais que o valor.</summary>
    public static (int Grupo, Valor Valor) Forca(Carta carta, Naipe mania) =>
        (carta.Naipe == mania ? 1 : 0, carta.Valor);

    // Mania vence naipe aberto, que vence qualquer outro; dentro do grupo, vale o valor.
    private static (int Grupo, Valor Valor) Chave(Carta carta, Naipe mania, Naipe naipeAberto) =>
        (carta.Naipe == mania ? 2 : carta.Naipe == naipeAberto ? 1 : 0, carta.Valor);
}
