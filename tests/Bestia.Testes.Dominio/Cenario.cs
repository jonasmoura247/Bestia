using Bestia.Dominio;
using Bestia.Dominio.Cartas;
using Bestia.Dominio.Jogadores;
using Bestia.Dominio.Partidas;

namespace Bestia.Testes.Dominio;

/// <summary>Não embaralha: Sortear(n) devolve n-1, então toda troca é consigo mesma e o distribuidor é o último assento.</summary>
internal sealed class SorteadorSemEmbaralhar : ISorteador
{
    public int Sortear(int limiteExclusivo) => limiteExclusivo - 1;
}

internal sealed class SorteadorComSemente(int semente) : ISorteador
{
    private readonly Random _aleatorio = new(semente);

    public int Sortear(int limiteExclusivo) => _aleatorio.Next(limiteExclusivo);
}

/// <summary>Três jogadores A (distribuidor), B e C; a decisão segue B, C, A e a mania é Espadas.</summary>
internal static class Cenario
{
    public static readonly Carta[] Fracas = [C(Valor.Quatro, Naipe.Copas), C(Valor.Cinco, Naipe.Copas), C(Valor.Seis, Naipe.Copas)];
    public static readonly Carta[] Fortes = [C(Valor.As, Naipe.Paus), C(Valor.Tres, Naipe.Paus), C(Valor.Dois, Naipe.Paus)];
    public static readonly Carta[] Medias = [C(Valor.Rei, Naipe.Ouros), C(Valor.Dama, Naipe.Ouros), C(Valor.Valete, Naipe.Ouros)];
    public static readonly Carta ManiaPadrao = C(Valor.Seis, Naipe.Espadas);
    public static readonly Carta[] Bau1 = [C(Valor.Sete, Naipe.Copas), C(Valor.Oito, Naipe.Copas), C(Valor.Nove, Naipe.Copas)];
    public static readonly Carta[] Bau2 = [C(Valor.Sete, Naipe.Paus), C(Valor.Oito, Naipe.Paus), C(Valor.Nove, Naipe.Paus)];

    public static Carta C(Valor valor, Naipe naipe) => new(naipe, valor);

    public static Mao Nova(
        Jogador a, Jogador b, Jogador c,
        Carta[]? maoA = null, Carta[]? maoB = null, Carta[]? maoC = null,
        int montantePendente = 0)
    {
        List<Carta> baralho = [.. maoA ?? Fracas, .. maoB ?? Fortes, .. maoC ?? Medias, ManiaPadrao, .. Bau1, .. Bau2];
        return new Mao([a, b, c], a, montantePendente, baralho);
    }
}
