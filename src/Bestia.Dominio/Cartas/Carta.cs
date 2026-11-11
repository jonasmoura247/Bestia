namespace Bestia.Dominio.Cartas;

public enum Naipe { Copas, Ouros, Espadas, Paus }

// Do mais fraco ao mais forte: o valor numérico do enum é a força da carta.
public enum Valor { Quatro, Cinco, Seis, Sete, Oito, Nove, Valete, Dama, Rei, Dois, Tres, As }

public readonly record struct Carta(Naipe Naipe, Valor Valor);
