namespace Bestia.Dominio.Jogadores;

public enum MotivoDaSaida { SemFichas, Inatividade, Desistencia }

public sealed class Jogador(string nome, bool ehBot = false, int fichas = ConfiguracaoDoJogo.FichasIniciais)
{
    public string Nome { get; } = nome;

    public bool EhBot { get; } = ehBot;

    public int Fichas { get; internal set; } = fichas;

    public bool EstaNaPartida { get; internal set; } = true;

    public MotivoDaSaida? MotivoDaSaida { get; internal set; }
}
