using Bestia.Aplicacao.Contratos;
using Bestia.Aplicacao.Modelos;

namespace Bestia.Aplicacao.Perfil;

public sealed record ItemDoHistorico(DateTime Quando, int Colocacao, int Jogadores, int FichasFinais, bool Interrompida);

public sealed record EstatisticasDoPerfil(int Partidas, int Vitorias, int FichasFinaisSomadas, IReadOnlyList<ItemDoHistorico> Recentes);

public sealed class PerfilServico(IRepositorio repositorio)
{
    private const int TamanhoDoHistorico = 10;

    public async Task<EstatisticasDoPerfil> ObterAsync(Guid contaId, CancellationToken ct = default)
    {
        var linhas = await repositorio.ListarAsync<PartidaJogador>(l => l.JogadorId == contaId, ct);
        var ids = linhas.Select(l => l.PartidaId).ToList();
        var partidas = await repositorio.ListarAsync<Partida>(p => ids.Contains(p.Id), ct);

        var historico = (from linha in linhas
                         join partida in partidas on linha.PartidaId equals partida.Id
                         select new ItemDoHistorico(
                             partida.IniciadaEm, linha.Colocacao ?? 0, partida.QuantidadeDeJogadores, linha.FichasFinais,
                             partida.Situacao != SituacaoDaPartida.Encerrada))
            .OrderByDescending(item => item.Quando)
            .ToList();
        var concluidas = historico.Where(item => !item.Interrompida).ToList();

        return new EstatisticasDoPerfil(
            concluidas.Count,
            concluidas.Count(item => item.Colocacao == 1),
            concluidas.Sum(item => item.FichasFinais),
            historico.Take(TamanhoDoHistorico).ToList());
    }
}
