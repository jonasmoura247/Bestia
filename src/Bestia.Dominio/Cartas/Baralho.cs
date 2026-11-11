namespace Bestia.Dominio.Cartas;

public static class Baralho
{
    public static IReadOnlyList<Carta> Criar() =>
        (from naipe in Enum.GetValues<Naipe>()
         from valor in Enum.GetValues<Valor>()
         select new Carta(naipe, valor)).ToList();
}
