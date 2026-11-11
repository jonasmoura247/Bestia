using Bestia.Aplicacao.Contratos;

namespace Bestia.Aplicacao.Partidas;

public sealed record PerfilDoJogador(Guid ContaId, string Apelido, string Cor, string Idioma, bool EmailConfirmado);

public sealed record ResumoDaSala(string Codigo, string Nome, int Jogadores, int Maximo, bool EmPartida);

public enum FaseDaSala { Espera, EmPartida, Encerrada }

/// <summary>Naipe e valor são os números dos enums do domínio; o navegador faz o desenho.</summary>
public sealed record CartaVisao(int Naipe, int Valor);

public sealed record AssentoVisao(
    int Assento, string Nome, string Cor, bool EhBot, bool Voce, int Fichas, bool EstaNaPartida,
    string? Saida, bool EhDistribuidor, string? Decisao, int Baus, int Vitorias);

public sealed record JogadaVisao(int Assento, CartaVisao? Carta, bool Virada);

public sealed record BauVisao(int Numero, int? Dono, bool Disponivel);

public sealed record DecisaoVisao(bool PodeFicar, bool PodeCorrer, bool JaPegouBau);

/// <summary>Mensagem do diário ou da faixa: a chave de texto e os argumentos; o navegador traduz.</summary>
public sealed record EventoVisao(string Chave, string[] Argumentos);

/// <summary>Foto da mesa na visão de UM jogador: ninguém recebe cartas alheias nem conteúdo de baú fechado.</summary>
public sealed record VisaoDaMesa(
    string Codigo, string Nome, string Fase, bool SouCriador, int Maximo, int Bots,
    IReadOnlyList<AssentoVisao> Assentos, int MeuAssento,
    int Mao, int Montante, int ValorDaRodada, CartaVisao? Mania,
    string? Etapa, int? Vez, int? Proximo, int? Abre, int Rodada,
    IReadOnlyList<CartaVisao> MinhaMao, IReadOnlyList<int> Permitidas,
    IReadOnlyList<BauVisao> Baus, DecisaoVisao? Decisao,
    IReadOnlyList<JogadaVisao> Jogadas, int? VencedorDaRodada, IReadOnlyList<int> Perdedores,
    int PrazoSegundos, DateTime? PrazoFimUtc, DateTime AgoraUtc,
    IReadOnlyList<EventoVisao> Diario, EventoVisao? Aviso);

public interface INotificadorDaMesa
{
    Task MesaAtualizadaAsync(Guid contaId, VisaoDaMesa visao);

    Task EmoteAsync(Guid contaId, int assento, int emote);
}

/// <summary>Executa um trabalho com um repositório novo (e depois o descarta); usado por serviços singleton.</summary>
public interface IUnidadeDeTrabalho
{
    Task ExecutarAsync(Func<IRepositorio, Task> trabalho, CancellationToken ct = default);
}
