using Bestia.Aplicacao.Contratos;
using Bestia.Aplicacao.Partidas;
using Bestia.Aplicacao.Privacidade;
using Bestia.Recursos;
using Microsoft.Extensions.Hosting;

namespace Bestia.Web.Configuracao;

/// <summary>Uma vez por dia: apaga registros de acesso antigos, tokens vencidos e trata contas inativas.</summary>
public sealed class RotinasDeRetencao(
    IUnidadeDeTrabalho trabalho, ICriptografia criptografia, IHashDeSenha hashDeSenha,
    IRelogio relogio, IEnviadorDeEmail enviadorDeEmail, ITextos textos, ILogger<RotinasDeRetencao> registro) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken parada)
    {
        while (true)
        {
            try
            {
                await trabalho.ExecutarAsync(
                    repositorio => new PrivacidadeServico(repositorio, criptografia, hashDeSenha, relogio, enviadorDeEmail, textos)
                        .ExecutarRotinasAsync(parada),
                    parada);
            }
            catch (Exception erro)
            {
                registro.LogError(erro, "Falha nas rotinas de retenção.");
            }

            try
            {
                await relogio.EsperarAsync(Intervalo, parada);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
